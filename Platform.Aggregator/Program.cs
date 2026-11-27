using Platform.Aggregator.Caching;
using Platform.Aggregator.Grpc;
using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Http.Resilience;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();                       // Serilog, OpenTelemetry, service discovery, CorrelationIdHandler, стійкість

// ---------- gRPC-клієнти (Лаб. 06): згенеровані з .proto сервісів, адреса — логічне ім'я ресурсу ----------
// "http://_grpc.catalog-api" = іменований endpoint "grpc" (HTTP/2) ресурсу catalog-api через service discovery.
// Фабрика HttpClient та сама, тож діють налаштування ServiceDefaults: service discovery, стійкість, CorrelationIdHandler.
builder.Services.AddGrpcClient<Bookings.Grpc.BookingService.BookingServiceClient>(o => o.Address = new Uri($"http://_grpc.{BookingsClient.ServiceName}"));
builder.Services.AddGrpcClient<Catalog.Grpc.CatalogService.CatalogServiceClient>(o => o.Address = new Uri($"http://_grpc.{CatalogClient.ServiceName}"));
builder.Services.AddGrpcClient<Reviews.Grpc.ReviewsService.ReviewsServiceClient>(o => o.Address = new Uri($"http://_grpc.{ReviewsClient.ServiceName}"));

// ---------- Redis: кеш агрегованих відповідей (TTL 30 с) ----------
if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("redis")))
    builder.AddRedisDistributedCache("redis", configureOptions: o =>
    {
        o.AbortOnConnectFail = false;
        o.ConnectTimeout = 2000;
        o.AsyncTimeout = 2000;
    });
else
    builder.Services.AddDistributedMemoryCache();       // без Aspire / у тестах

// ---------- Typed HttpClients (Лаб. 05): залишені для порівняння HTTP і gRPC (?transport=http) ----------
builder.Services.AddHttpClient<BookingsClient>(c => c.BaseAddress = new Uri($"https+http://{BookingsClient.ServiceName}"));
builder.Services.AddHttpClient<CatalogClient>(c => c.BaseAddress = new Uri($"https+http://{CatalogClient.ServiceName}"));
builder.Services.AddHttpClient<ReviewsClient>(c => c.BaseAddress = new Uri($"https+http://{ReviewsClient.ServiceName}"));

// Агрегатор має відповідати швидко навіть при недоступному сервісі: менше повторів і коротші таймаути,
// ніж у стандартної політики ServiceDefaults (там 3 повтори з експоненційною паузою).
builder.Services.ConfigureAll<HttpStandardResilienceOptions>(o =>
{
    o.Retry.MaxRetryAttempts = 1;
    o.Retry.Delay = TimeSpan.FromMilliseconds(200);
    o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
    o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
    o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
});

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
