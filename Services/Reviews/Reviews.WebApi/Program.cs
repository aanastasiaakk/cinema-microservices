using System.Globalization;
using System.Reflection;
using MongoDB.Driver;
using Reviews.Application;
using Reviews.Infrastructure;
using Reviews.WebApi.Contracts;
using Reviews.WebApi.Infrastructure;
using Serilog;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "reviews-api"));

// ---------- Шари: кожен надає свій метод розширення ----------
builder.Services
    .AddApplication(builder.Configuration)         // MediatR + behaviors, валідатори, AutoMapper (Application)
    .AddInfrastructure(builder.Configuration);     // MongoDB: мапінг, контекст, репозиторії, запити, індекси, сідінг

// AutoMapper-профіль HTTP-контрактів (WebApi → команди)
builder.Services.AddAutoMapper(cfg =>
{
    var key = builder.Configuration["LuckyPenny:LicenseKey"];
    if (!string.IsNullOrWhiteSpace(key)) cfg.LicenseKey = key;
}, typeof(RequestProfile).Assembly);

builder.Services.AddControllers();
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddHealthChecks()
    .AddMongoDb(sp => sp.GetRequiredService<IMongoClient>(), name: "mongodb", timeout: TimeSpan.FromSeconds(3));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new() { Title = "Cinema Reviews API", Version = "v1",
        Description = "Відгуки й обговорення фільмів (Проєкт №3: Clean Architecture + CQRS/MediatR + MongoDB)" });
    var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml)) o.IncludeXmlComments(xml);
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging(o =>
    o.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0} ms");
app.UseExceptionHandler();

if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();

public partial class Program;
