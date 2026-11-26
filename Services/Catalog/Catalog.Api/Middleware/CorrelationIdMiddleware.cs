using Serilog.Context;

namespace Catalog.Api.Middleware;

/// <summary>
/// Кореляційний Id: із заголовка X-Correlation-Id (або TraceIdentifier), повертається у відповіді
/// і додається до КОЖНОГО запису логу як структурована властивість CorrelationId (разом з RequestPath).
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 64)
            correlationId = context.TraceIdentifier;

        context.Response.Headers[HeaderName] = correlationId;
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestPath", context.Request.Path.Value))
        {
            await next(context);
        }
    }
}
