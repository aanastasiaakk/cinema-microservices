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

    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.AddSerilogDefaults();
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();
        builder.Services.AddHttpContextAccessor();
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
                                          && !ctx.Request.Path.StartsWithSegments(AlivenessEndpointPath))
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation();                                        // Dapper (Лаб. 02) і EF Core (Лаб. 03)
            });

        // адресу OTLP-колектора (Aspire Dashboard) задає AppHost; експорт батчевий і не блокує запити
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            builder.Services.AddOpenTelemetry().UseOtlpExporter();

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
        return builder;
    }

    /// <summary>/health — усі перевірки (у т. ч. БД сервісу), /alive — лише «процес живий».</summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks(HealthEndpointPath);
        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });
        return app;
    }

    /// <summary>Наскрізний CorrelationId: ставити першим middleware (у Gateway — до MapReverseProxy).</summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();
}
