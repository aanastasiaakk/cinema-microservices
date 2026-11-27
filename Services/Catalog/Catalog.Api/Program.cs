using System.Globalization;
using System.Reflection;
using Catalog.Api.Middleware;
using Catalog.Bll;
using Catalog.Dal;
using Catalog.Dal.Interfaces;
using Catalog.Dal.Seeding;
using Microsoft.EntityFrameworkCore;
using Serilog;

// Повідомлення API (валідація, формат чисел) не залежать від локалі машини (uk-UA пише "0,01")
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

// ---------- Платформа (Лаб. 05): Serilog, OpenTelemetry, health checks, service discovery, CorrelationId ----------
// Рівні логів (у т. ч. SQL-команди EF Core) уточнює секція "Serilog" в appsettings.json.
builder.AddServiceDefaults();

// ---------- EF Core: рядок підключення з appsettings (Aspire перевизначає ConnectionStrings__CinemaCatalog) ----------
var connectionString = builder.Configuration.GetConnectionString("CinemaCatalog")
    ?? throw new InvalidOperationException("Connection string 'CinemaCatalog' is missing");
builder.Services.AddDbContext<CatalogDbContext>(o => o
    .UseSqlServer(connectionString, sql => sql
        .EnableRetryOnFailure(3)
        // свідомо ОДИН запит з JOIN-ами для Include (Eager Loading = 1 SQL-команда в лозі)
        .UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery))
    .EnableSensitiveDataLogging(builder.Environment.IsDevelopment()));   // значення параметрів SQL — лише в Development

// ---------- DI: Unit of Work і сервіси — Scoped (один DbContext на запит) ----------
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddCatalogBll(builder.Configuration["AutoMapper:LicenseKey"]);

builder.Services.AddControllers();

// ---------- Помилки: ProblemDetails + один глобальний обробник ----------
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ---------- Swagger ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new() { Title = "Cinema Catalog API", Version = "v1",
        Description = "Каталог фільмів, жанрів і розкладу (Проєкт №2: EF Core Code First)" });
    var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml)) o.IncludeXmlComments(xml);
});

var app = builder.Build();

// ---------- Міграції + ідемпотентний сідінг (вимикаються в production через Database:*) ----------
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    await db.Database.MigrateAsync();                                 // HasData-довідники — у міграції
    if (app.Configuration.GetValue("Database:SeedOnStartup", !app.Environment.IsProduction()))
        await DbInitializer.SeedAsync(db, app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer"));
}

// Порядок: кореляційний Id → лог запиту (бачить фінальний статус) → глобальний обробник помилок
app.UseCorrelationId();
app.UseSerilogRequestLogging(o =>
{
    o.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0} ms";
    o.EnrichDiagnosticContext = (diag, http) =>
    {
        diag.Set("RequestHost", http.Request.Host.Value ?? "");
        diag.Set("UserId", http.User.Identity?.IsAuthenticated == true ? http.User.Identity.Name ?? "" : "anonymous");
    };
});
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapDefaultEndpoints();
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();

/// <summary>Для WebApplicationFactory в API-тестах.</summary>
public partial class Program;
