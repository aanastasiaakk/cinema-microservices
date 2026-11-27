using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Http.Resilience;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();                       // Serilog, OpenTelemetry, service discovery, CorrelationIdHandler, стійкість

// Typed HttpClients: логічні імена ресурсів Aspire замість localhost:port
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
