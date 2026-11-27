using Serilog.Context;

namespace Reviews.WebApi.Infrastructure;

/// <summary>X-Correlation-Id → у відповідь і в кожен запис логу (CorrelationId, RequestPath).</summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 64) correlationId = context.TraceIdentifier;
        context.Response.Headers[HeaderName] = correlationId;
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestPath", context.Request.Path.Value))
            await next(context);
    }
}
