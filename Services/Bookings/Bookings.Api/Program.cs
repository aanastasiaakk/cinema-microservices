using System.Reflection;
using Bookings.Api.Grpc;
using Bookings.Api.Middleware;
using Bookings.Bll.Interfaces;
using Bookings.Bll.Mapping;
using Bookings.Bll.Services;
using Bookings.Dal;
using Bookings.Dal.Infrastructure;
using Bookings.Dal.Interfaces;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---------- Платформа (Лаб. 05): Serilog, OpenTelemetry, health checks, service discovery, CorrelationId ----------
builder.AddServiceDefaults();

// ---------- Рядок підключення: appsettings.json, перевизначається змінною оточення ----------
// ConnectionStrings__CinemaBooking=...   (так само його передає Aspire через WithReference)
var connectionString = builder.Configuration.GetConnectionString("CinemaBooking")
    ?? throw new InvalidOperationException("Connection string 'CinemaBooking' is missing");

// ---------- DI ----------
// Unit of Work — Scoped: один екземпляр і ОДНЕ з'єднання на HTTP-запит (DisposeAsync у кінці запиту)
builder.Services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(connectionString));
// Сервіси BLL — Scoped (залежать від UoW)
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IShowtimeService, ShowtimeService>();
builder.Services.AddSingleton(TimeProvider.System);

// AutoMapper: усі профілі з BLL одним викликом (централізовано)
builder.Services.AddAutoMapper(cfg =>
{
    cfg.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"];
}, typeof(BookingProfile).Assembly);

// ---------- gRPC (Лаб. 06): окремий endpoint лише HTTP/2 (Kestrel:Endpoints:grpc в appsettings.json) ----------
builder.Services.AddGrpc(o =>
{
    o.Interceptors.Add<GrpcLoggingInterceptor>();
    o.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

builder.Services.AddControllers();

// ---------- Помилки: ProblemDetails + один глобальний обробник ----------
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

// ---------- OpenAPI / Swagger ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new() { Title = "Cinema Bookings API", Version = "v1",
        Description = "Мікросервіс бронювання квитків (Проєкт №1: ADO.NET + Dapper)" });
    var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml)) o.IncludeXmlComments(xml);
});

var app = builder.Build();

// Порожня БД у контейнері Aspire -> застосувати скрипти Лаб. 01 (ідемпотентні)
if (app.Configuration.GetValue<bool>("Database:ApplyScripts"))
{
    var scripts = Path.Combine(AppContext.BaseDirectory, "db", "p1");
    await DatabaseInitializer.ApplyAsync(connectionString, scripts,
        app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer"));
}

// Порядок: кореляційний Id → лог запиту (бачить фінальний статус) → глобальний обробник помилок
app.UseCorrelationId();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapGrpcService<BookingGrpcService>();
app.MapDefaultEndpoints();
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();

/// <summary>Для WebApplicationFactory в API-тестах.</summary>
public partial class Program;
