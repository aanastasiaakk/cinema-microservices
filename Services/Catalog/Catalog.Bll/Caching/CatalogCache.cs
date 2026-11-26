using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Catalog.Bll.Caching;

/// <summary>Ключі кешу довідників каталогу.</summary>
public static class CacheKeys
{
    public const string Genres = "catalog:genres";
    public const string Halls = "catalog:halls";
}

/// <summary>
/// Кеш довідників (жанри, зали) в IMemoryCache: вони змінюються рідко, а читаються з кожною
/// сторінкою каталогу. Після зміни довідника сервіс викликає <see cref="Invalidate"/>.
/// </summary>
public sealed class CatalogCache(IMemoryCache cache, ILogger<CatalogCache> logger)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken ct)
    {
        if (cache.TryGetValue(key, out T? cached) && cached is not null)
        {
            logger.LogDebug("Cache hit {CacheKey}", key);
            return cached;
        }

        logger.LogDebug("Cache miss {CacheKey}", key);
        var value = await factory(ct);
        cache.Set(key, value, Ttl);
        return value;
    }

    public void Invalidate(string key)
    {
        cache.Remove(key);
        logger.LogInformation("Cache invalidated {CacheKey}", key);
    }
}
