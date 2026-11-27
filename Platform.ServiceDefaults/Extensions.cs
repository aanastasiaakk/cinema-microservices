using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Спільні налаштування КОЖНОГО сервісу платформи (як eShop.ServiceDefaults):
/// Serilog (структуровані логи → Console у JSON і далі в OpenTelemetry), OpenTelemetry (трасування
/// HTTP, SQL, MongoDB і власних ActivitySource, метрики, експорт OTLP в Aspire Dashboard),
/// health checks, service discovery, стійкість HttpClient і наскрізний CorrelationId.
/// </summary>
public static class Extensions
{
    /// <summary>Префікс власних ActivitySource бізнес-операцій: Cinema.Bookings, Cinema.Catalog, Cinema.Reviews…</summary>
    public const string CinemaActivitySourcePrefix = "Cinema.";

    /// <summary>Джерело spans драйвера MongoDB (DiagnosticsActivityEventSubscriber).</summary>
    public const string MongoActivitySource = "MongoDB.Driver.Core.Extensions.DiagnosticSources";

    /// <summary>Meter-и бізнес-метрик сервісів (Лаб. 07) — назва Meter = назва сервісу.</summary>
    public static readonly string[] BusinessMeters = ["Bookings.Api", "Catalog.Api", "Reviews.Api", "Platform.Aggregator"];

    /// <summary>Теги health checks: live — лише процес; ready — залежності, без яких сервіс не приймає трафік.</summary>
    public const string LiveTag = "live";
    public const string ReadyTag = "ready";

    private const string HealthEndpointPath = "/health";
    private const string LivenessEndpointPath = "/health/live";
    private const string ReadinessEndpointPath = "/health/ready";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.AddSerilogDefaults();
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<CacheMetrics>();                // cache.hits / cache.misses (Лаб. 06)
        builder.Services.AddTransient<CorrelationIdHandler>();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();                  // retry, timeout, circuit breaker (детально — Лаб. 07)
            http.AddServiceDiscovery();                           // http://catalog-api → реальна адреса від Aspire
            http.AddHttpMessageHandler<CorrelationIdHandler>();   // X-Correlation-Id у вихідні запити
        });
        return builder;
    }

    /// <summary>
    /// Serilog замість стандартного провайдера: enrichers ServiceName, Environment, MachineName, ThreadId,
    /// FromLogContext (CorrelationId); рівень Debug у Development і Information в інших середовищах;
    /// шумна інфраструктура приглушена; Console у CompactJsonFormatter. writeToProviders: true передає кожен
    /// запис також стандартним провайдерам — серед них OpenTelemetry → вкладка Structured logs у Dashboard.
    /// Секція "Serilog" в appsettings сервісу може уточнити рівні (застосовується після значень за замовчуванням).
    /// </summary>
    public static TBuilder AddSerilogDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? builder.Environment.ApplicationName;
        var isDevelopment = builder.Environment.IsDevelopment();

        builder.Services.AddSerilog((services, cfg) => cfg
            .MinimumLevel.Is(isDevelopment ? LogEventLevel.Debug : LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .MinimumLevel.Override("Polly", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Extensions.Http", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Extensions.ServiceDiscovery", LogEventLevel.Warning)
            .MinimumLevel.Override("Grpc", LogEventLevel.Warning)                 // власні логи gRPC — interceptor
            .MinimumLevel.Override("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogEventLevel.Fatal)
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("ServiceName", serviceName)
            .Enrich.WithEnvironmentName()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .WriteTo.Console(new RenderedCompactJsonFormatter()),
            writeToProviders: true);

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        var samplingRatio = builder.Configuration.GetValue("OpenTelemetry:SamplingRatio", 0.25);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = builder.Environment.EnvironmentName
            }))
            .WithMetrics(metrics => metrics
                .AddMeter(CacheMetrics.MeterName)                                      // cache.hits / cache.misses (L1/L2)
                .AddMeter(BusinessMeters)                                              // бізнес-метрики сервісів (Лаб. 07)
                .AddMeter("Polly")                                                     // resilience.polly.* (OnRetry, OnCircuitOpened…)
                .AddMeter(CinemaActivitySourcePrefix + "*")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .WithTracing(tracing =>
            {
                tracing
                    .SetSampler(builder.Environment.IsDevelopment()
                        ? new AlwaysOnSampler()                                            // 100 % у Development
                        : new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio)))
                    .AddSource(builder.Environment.ApplicationName)
                    .AddSource(CinemaActivitySourcePrefix + "*")                           // власні spans бізнес-операцій
                    .AddSource(MongoActivitySource)                                        // команди MongoDB (Лаб. 04)
                    .AddAspNetCoreInstrumentation(o =>
                        o.Filter = ctx => !ctx.Request.Path.StartsWithSegments(HealthEndpointPath)
                                          && !ctx.Request.Path.StartsWithSegments("/grpc.health.v1.Health")
                                          && !ctx.Request.Path.StartsWithSegments(AlivenessEndpointPath))
                    .AddHttpClientInstrumentation()                                        // у т. ч. клієнтські gRPC-виклики
                    .AddSqlClientInstrumentation();                                        // Dapper (Лаб. 02) і EF Core (Лаб. 03)
                // команди Redis (Лаб. 06) трасує сама Aspire-інтеграція AddRedisDistributedCache
            });

        // адресу OTLP-колектора (Aspire Dashboard) задає AppHost; експорт батчевий і не блокує запити
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            builder.Services.AddOpenTelemetry().UseOtlpExporter();

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // liveness: лише «процес живий», без зовнішніх залежностей — має відповідати < 100 мс
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("process is running"), [LiveTag]);
        return builder;
    }

    /// <summary>
    /// Health endpoints у ВСІХ середовищах (їх використовують Aspire і Kubernetes, Лаб. 10):
    ///  • /health/live  — liveness: лише перевірки з тегом live (процес), збій → перезапуск;
    ///  • /health/ready — readiness: перевірки з тегом ready (БД, кеш, залежності) з JSON-деталями, збій → поза балансуванням;
    ///  • /health       — усі перевірки з JSON-деталями (діагностика);  /alive — сумісність із шаблоном Aspire.
    /// Degraded (наприклад, недоступний Redis) повертає 200: сервіс працює, але без необов'язкової залежності.
    /// </summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks(LivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains(LiveTag),
            ResponseWriter = HealthResponseWriter.WriteJsonAsync
        });
        app.MapHealthChecks(ReadinessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains(ReadyTag),
            ResponseWriter = HealthResponseWriter.WriteJsonAsync
        });
        app.MapHealthChecks(HealthEndpointPath, new HealthCheckOptions { ResponseWriter = HealthResponseWriter.WriteJsonAsync });
        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions { Predicate = r => r.Tags.Contains(LiveTag) });
        return app;
    }

    /// <summary>Наскрізний CorrelationId: ставити першим middleware (у Gateway — до MapReverseProxy).</summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();
}
