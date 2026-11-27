using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Reviews.Application.Caching;

/// <summary>Запит, результат якого можна кешувати (read-only, часто читається).</summary>
public interface ICacheableQuery
{
    string CacheKey { get; }
    TimeSpan Expiration { get; }
}

/// <summary>Ключі кешу.</summary>
public static class CacheKeys
{
    public static string MovieRating(long movieId) => $"reviews:rating:{movieId}";
}

/// <summary>Інвалідація кешу після команд, що змінюють дані.</summary>
public interface ICacheInvalidator
{
    void Invalidate(string key);
}

public sealed class MemoryCacheInvalidator(IMemoryCache cache, ILogger<MemoryCacheInvalidator> logger) : ICacheInvalidator
{
    public void Invalidate(string key)
    {
        cache.Remove(key);
        logger.LogDebug("Cache invalidated {CacheKey}", key);
    }
}

/// <summary>Кешує відповіді <see cref="ICacheableQuery"/> в IMemoryCache (детально — Лаб. 06).</summary>
public sealed class CachingBehavior<TRequest, TResponse>(IMemoryCache cache, ILogger<CachingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not ICacheableQuery cacheable) return await next(ct);

        if (cache.TryGetValue(cacheable.CacheKey, out TResponse? cached) && cached is not null)
        {
            logger.LogDebug("Cache hit {CacheKey}", cacheable.CacheKey);
            return cached;
        }

        var response = await next(ct);
        if (response is not null) cache.Set(cacheable.CacheKey, response, cacheable.Expiration);
        return response;
    }
}
