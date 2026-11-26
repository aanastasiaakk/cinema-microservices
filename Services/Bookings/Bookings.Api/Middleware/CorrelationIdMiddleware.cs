using Serilog.Context;

namespace Bookings.Api.Middleware;

/// <summary>
/// Кореляційний ідентифікатор: береться із заголовка X-Correlation-Id (або генерується),
/// повертається у відповіді і додається до кожного запису логу (Serilog LogContext).
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
        {
            await next(context);
        }
    }
}
