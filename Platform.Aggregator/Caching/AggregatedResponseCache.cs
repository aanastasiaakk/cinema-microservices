using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Caching;

/// <summary>
/// Кеш агрегованої картки в Redis (IDistributedCache, Aspire AddRedisDistributedCache):
/// ключ з параметрів запиту "aggregator:booking:{id}:full:{transport}", перевірка ДО паралельних викликів,
/// короткий TTL 30 с — коротший за кеші сервісів, щоб агрегат не виглядав «свіжішим» за свої джерела.
/// Часткові відповіді (з warnings) НЕ кешуються. Redis недоступний — працюємо без кешу.
/// </summary>
public sealed class AggregatedResponseCache(IDistributedCache cache, CacheMetrics metrics, ILogger<AggregatedResponseCache> logger)
{
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);
    private const string CacheName = "aggregated-booking-card";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // запобіжник: після помилки Redis 30 с не чекаємо його таймаутів
    private static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(30);
    private long _disabledUntilTicks;
    private bool Available => DateTime.UtcNow.Ticks >= Interlocked.Read(ref _disabledUntilTicks);

    private void Trip(string key, Exception ex)
    {
        var wasAvailable = Available;
        Interlocked.Exchange(ref _disabledUntilTicks, DateTime.UtcNow.Add(RetryAfter).Ticks);
        if (wasAvailable)
            logger.LogWarning("Redis unavailable for aggregator cache ({CacheKey}): {Error}. Cache bypassed for {RetryAfter}", key, ex.Message, RetryAfter);
    }

    public static string Key(long bookingId, string transport) => $"aggregator:booking:{bookingId}:full:{transport}";

    public async Task<FullBookingDto?> TryGetAsync(string key, CancellationToken ct)
    {
        if (!Available) return null;
        try
        {
            var bytes = await cache.GetAsync(key, ct);
            if (bytes is null)
            {
                metrics.Miss(CacheMetrics.L2, CacheName);
                logger.LogDebug("Aggregator cache miss {CacheKey}", key);
                return null;
            }
            metrics.Hit(CacheMetrics.L2, CacheName);
            logger.LogInformation("Aggregator cache hit {CacheKey} ({SizeBytes} bytes), downstream calls skipped", key, bytes.Length);
            return JsonSerializer.Deserialize<FullBookingDto>(bytes, Json);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Trip(key, ex);
            return null;
        }
    }

    public async Task SetAsync(string key, FullBookingDto card, CancellationToken ct)
    {
        if (card.IsPartial)
        {
            logger.LogInformation("Partial booking card {CacheKey} is not cached ({WarningsCount} warnings)", key, card.Warnings.Count);
            return;
        }
        if (!Available) return;
        try
        {
            await cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(card, Json),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl }, ct);
            logger.LogDebug("Aggregator cached {CacheKey} for {Ttl}", key, Ttl);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Trip(key, ex);
        }
    }
}
