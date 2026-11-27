using MediatR;
using Microsoft.Extensions.Logging;

namespace Reviews.Application.Common.Behaviors;

/// <summary>
/// 1-й (зовнішній) behavior: структуроване логування кожної команди/запиту і результату.
/// Якщо далі ValidationBehavior відхилить запит, у лозі буде "Handling" + "rejected",
/// але НЕ буде запису PerformanceBehavior "Handler … executed" — тобто обробник не викликався.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        logger.LogInformation("Handling {RequestName} {@Request}", name, request);
        try
        {
            var response = await next(ct);
            logger.LogInformation("Handled {RequestName} -> {ResponseType}", name, typeof(TResponse).Name);
            return response;
        }
        catch (Exception ex)
        {
            // без stack trace: виняток один раз логує глобальний обробник WebApi
            logger.LogWarning("{RequestName} rejected: {ExceptionType} {ExceptionMessage}", name, ex.GetType().Name, ex.Message);
            throw;
        }
    }
}
