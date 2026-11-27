using System.Diagnostics;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();                                    // Serilog, OpenTelemetry, health, service discovery

// YARP: маршрути й кластери — у секції ReverseProxy (appsettings.json); адреси http://<ім'я-ресурсу>
// перетворює на реальні адреси service discovery Aspire.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

var app = builder.Build();

// 1) CorrelationId: генерується тут, на вході в систему, ДО MapReverseProxy — YARP копіює заголовок у проксований запит
app.UseCorrelationId();

// 2) Логування маршрутизації: метод, шлях, маршрут, кластер, адреса призначення, статус, тривалість, розмір, IP клієнта
app.Use(async (context, next) =>
{
    var sw = Stopwatch.StartNew();
    await next(context);
    sw.Stop();

    var proxy = context.Features.Get<IReverseProxyFeature>();
    var error = context.Features.Get<IForwarderErrorFeature>();
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Gateway.Routing");

    if (proxy is null)
    {
        logger.LogDebug("{Method} {Path} handled by gateway itself: {StatusCode} in {ElapsedMs} ms",
            context.Request.Method, context.Request.Path, context.Response.StatusCode, sw.ElapsedMilliseconds);
        return;
    }

    var route = proxy.Route.Config.RouteId;
    var cluster = proxy.Cluster?.Config.ClusterId;
    var destination = proxy.ProxiedDestination?.Model.Config.Address;
    if (error is not null)
        logger.LogWarning(error.Exception,
            "{Method} {Path} -> route {Route} cluster {Cluster} ({Destination}) FAILED: {ForwarderError}, {StatusCode} in {ElapsedMs} ms",
            context.Request.Method, context.Request.Path, route, cluster, destination, error.Error,
            context.Response.StatusCode, sw.ElapsedMilliseconds);
    else
        logger.LogInformation(
            "{Method} {Path} -> route {Route} cluster {Cluster} ({Destination}): {StatusCode} in {ElapsedMs} ms, {ResponseBytes} bytes, client {ClientIp}",
            context.Request.Method, context.Request.Path, route, cluster, destination, context.Response.StatusCode,
            sw.ElapsedMilliseconds, context.Response.ContentLength, context.Connection.RemoteIpAddress?.ToString());
});

app.MapReverseProxy();
app.MapDefaultEndpoints();

// довідка: які маршрути обслуговує Gateway
app.MapGet("/", (IConfiguration config) => Results.Ok(new
{
    gateway = "Cinema API Gateway",
    routes = config.GetSection("ReverseProxy:Routes").GetChildren()
        .Select(r => new { route = r.Key, path = r["Match:Path"], cluster = r["ClusterId"], order = r["Order"] })
}));

app.Run();

public partial class Program;
