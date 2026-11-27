using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// X-Correlation-Id — бізнес-ідентифікатор звернення (на відміну від технічного TraceId OpenTelemetry):
/// береться з вхідного запиту або генерується (GUID) на вході в систему — зазвичай у Gateway;
/// записується назад у заголовки запиту (його побачать YARP і CorrelationIdHandler), у відповідь
/// і в LogContext, тож потрапляє в УСІ логи запиту. Межі запиту логуються (boundary logging).
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string LogProperty = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();
        var generated = false;
        if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 64)
        {
            correlationId = Guid.NewGuid().ToString();
            context.Request.Headers[HeaderName] = correlationId;      // далі його бачать YARP і HttpClient
            generated = true;
        }

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });
        System.Diagnostics.Activity.Current?.SetTag("correlation.id", correlationId);

        using (LogContext.PushProperty(LogProperty, correlationId))
        {
            if (generated)
                logger.LogInformation("CorrelationId {CorrelationId} generated for {Method} {Path}", correlationId,
                    context.Request.Method, context.Request.Path);
            logger.LogDebug("Request started {Method} {Path}", context.Request.Method, context.Request.Path);
            await next(context);
            logger.LogDebug("Request finished {Method} {Path} with {StatusCode}", context.Request.Method,
                context.Request.Path, context.Response.StatusCode);
        }
    }
}

/// <summary>DelegatingHandler: той самий X-Correlation-Id у кожен вихідний HTTP-запит (Aggregator → сервіси).</summary>
public sealed class CorrelationIdHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var id = accessor.HttpContext?.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        if (!string.IsNullOrEmpty(id) && !request.Headers.Contains(CorrelationIdMiddleware.HeaderName))
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, id);
        return base.SendAsync(request, ct);
    }
}
