using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Reviews.Application.Common.Behaviors;

/// <summary>
/// Останній behavior (найближчий до обробника): вимірює час обробника разом із запитами до MongoDB
/// і пише попередження, якщо він довший за поріг (Performance:SlowRequestMs, за замовчуванням 500 мс).
/// </summary>
public sealed class PerformanceBehavior<TRequest, TResponse>(
    ILogger<PerformanceBehavior<TRequest, TResponse>> logger, IConfiguration configuration)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    private readonly long _thresholdMs = long.TryParse(configuration["Performance:SlowRequestMs"], out var ms) ? ms : 500;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var response = await next(ct);
        sw.Stop();

        if (sw.ElapsedMilliseconds > _thresholdMs)
            logger.LogWarning("Slow handler {RequestName}: {ElapsedMs} ms (threshold {ThresholdMs} ms) {@Request}",
                typeof(TRequest).Name, sw.ElapsedMilliseconds, _thresholdMs, request);
        else
            logger.LogInformation("Handler {RequestName} executed in {ElapsedMs} ms", typeof(TRequest).Name, sw.ElapsedMilliseconds);

        return response;
    }
}
