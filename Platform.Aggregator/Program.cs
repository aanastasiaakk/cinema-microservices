using Platform.Aggregator.HealthChecks;
using Platform.Aggregator.Metrics;
using Platform.Aggregator.Resilience;
using Platform.Aggregator.Caching;
using Platform.Aggregator.Grpc;
using System.Reflection;
using System.Text.Json.Serialization;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();                       // Serilog, OpenTelemetry, service discovery, CorrelationIdHandler, стійкість

// ---------- gRPC-клієнти (Лаб. 06): згенеровані з .proto сервісів, адреса — логічне ім'я ресурсу ----------
// "http://_grpc.catalog-api" = іменований endpoint "grpc" (HTTP/2) ресурсу catalog-api через service discovery.
// Resilience (Лаб. 07): стандартний обробник ServiceDefaults замінено (RemoveAllResilienceHandlers) на
// політику сервісу + retry за gRPC-статусами (ServiceConfig). Обґрунтування — Resilience/ServicePolicies.cs і README.
builder.Services.AddGrpcClient<Bookings.Grpc.BookingService.BookingServiceClient>(o => o.Address = new Uri($"http://_grpc.{BookingsClient.ServiceName}"))
    .AddServiceResilience(ServicePolicy.Bookings).AddGrpcRetryPolicy(ServicePolicy.Bookings);
builder.Services.AddGrpcClient<Catalog.Grpc.CatalogService.CatalogServiceClient>(o => o.Address = new Uri($"http://_grpc.{CatalogClient.ServiceName}"))
    .AddServiceResilience(ServicePolicy.Catalog).AddGrpcRetryPolicy(ServicePolicy.Catalog);
builder.Services.AddGrpcClient<Reviews.Grpc.ReviewsService.ReviewsServiceClient>(o => o.Address = new Uri($"http://_grpc.{ReviewsClient.ServiceName}"))
    .AddServiceResilience(ServicePolicy.Reviews).AddGrpcRetryPolicy(ServicePolicy.Reviews);

// ---------- Redis: кеш агрегованих відповідей (TTL 30 с) ----------
if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("redis")))
    builder.AddRedisDistributedCache("redis",
        settings => settings.DisableHealthChecks = true,     // власний health check redis-cache зі статусом Degraded
        o =>
        {
            o.AbortOnConnectFail = false;
            o.ConnectTimeout = 2000;
            o.AsyncTimeout = 2000;
        });
else
    builder.Services.AddDistributedMemoryCache();       // без Aspire / у тестах

// ---------- Typed HttpClients (Лаб. 05): для порівняння HTTP і gRPC (?transport=http), ті самі політики ----------
builder.Services.AddHttpClient<BookingsClient>(c => c.BaseAddress = new Uri($"https+http://{BookingsClient.ServiceName}"))
    .AddServiceResilience(ServicePolicy.Bookings);
builder.Services.AddHttpClient<CatalogClient>(c => c.BaseAddress = new Uri($"https+http://{CatalogClient.ServiceName}"))
    .AddServiceResilience(ServicePolicy.Catalog);
builder.Services.AddHttpClient<ReviewsClient>(c => c.BaseAddress = new Uri($"https+http://{ReviewsClient.ServiceName}"))
    .AddServiceResilience(ServicePolicy.Reviews);

// ---------- Health checks (Лаб. 07): стан downstream-сервісів через gRPC Health Checking Protocol ----------
builder.AddDownstreamHealthChecks();
builder.Services.AddSingleton<AggregatorMetrics>();

builder.Services.AddScoped<BookingCardAggregator>();
builder.Services.AddScoped<GrpcBookingCardSource>();
builder.Services.AddScoped<HttpBookingCardSource>();
builder.Services.AddSingleton<AggregatedResponseCache>();

builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);   // camelCase за замовчуванням
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new() { Title = "Cinema Aggregator", Version = "v1",
        Description = "Композиція даних bookings-api + catalog-api + reviews-api (Aggregator Pattern)" });
    var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml)) o.IncludeXmlComments(xml);
});

var app = builder.Build();

app.UseCorrelationId();
app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapControllers();
app.MapDefaultEndpoints();
app.Run();

public partial class Program;
