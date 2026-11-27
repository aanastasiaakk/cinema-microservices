using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Catalog.Bll.Caching;

/// <summary>Дворівневий кеш L1 (IMemoryCache) → L2 (IDistributedCache / Redis) → джерело (БД).</summary>
public interface ITwoLevelCache
{
    /// <summary>Cache-aside: L1 → L2 → factory (БД); на зворотному шляху заповнюються L2 і L1.</summary>
    Task<T> GetOrCreateAsync<T>(string key, CachePolicy policy, Func<CancellationToken, Task<T>> factory, CancellationToken ct)
        where T : class;

    /// <summary>Інвалідація СИНХРОННО з операцією запису: ключі видаляються з L1 і L2, причина логується.</summary>
    Task InvalidateAsync(string reason, CancellationToken ct, params string[] keys);
}

/// <summary>
/// Ручна реалізація дворівневого кешу (замість HybridCache — щоб механізм було видно):
///  • L1 — IMemoryCache процесу: найшвидший, з Size (кеш має SizeLimit), sliding + absolute expiration;
///  • L2 — Redis через IDistributedCache (Aspire AddRedisDistributedCache): спільний для всіх екземплярів,
///    DTO серіалізуються System.Text.Json, TTL L2 довший за L1;
///  • недоступність Redis НЕ ламає сервіс: помилка L2 логується, дані беруться з БД (повільніше).
/// Кожне звернення рахується в метриках cache.hits / cache.misses з рівнем L1/L2.
/// </summary>
public sealed class TwoLevelCache(IMemoryCache memory, IDistributedCache distributed, CatalogCacheMetrics metrics,
                                  ILogger<TwoLevelCache> logger) : ITwoLevelCache
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Простий «запобіжник» L2: після помилки Redis наступні 30 с L2 оминається — інакше кожен запит
    /// чекав би таймаут з'єднання. Сервіс працює з L1 і БД (повільніше), потім знову пробує Redis.
    /// </summary>
    private static readonly TimeSpan L2RetryAfter = TimeSpan.FromSeconds(30);
    private long _l2DisabledUntilTicks;

    private bool L2Available => DateTime.UtcNow.Ticks >= Interlocked.Read(ref _l2DisabledUntilTicks);

    private void TripL2(string operation, string key, Exception ex)
    {
        var wasAvailable = L2Available;
        Interlocked.Exchange(ref _l2DisabledUntilTicks, DateTime.UtcNow.Add(L2RetryAfter).Ticks);
        if (wasAvailable)
            logger.LogWarning("Redis (L2) unavailable during {Operation} {CacheKey}: {Error}. L2 bypassed for {RetryAfter}, serving from L1/database",
                operation, key, ex.Message, L2RetryAfter);
    }

    public async Task<T> GetOrCreateAsync<T>(string key, CachePolicy policy, Func<CancellationToken, Task<T>> factory,
                                             CancellationToken ct) where T : class
    {
        // ---- L1: пам'ять процесу ----
        if (memory.TryGetValue(key, out T? fromMemory) && fromMemory is not null)
        {
            metrics.Hit("L1", policy.Name);
            logger.LogDebug("Cache hit L1 {CacheKey}", key);
            return fromMemory;
        }
        metrics.Miss("L1", policy.Name);

        // ---- L2: Redis ----
        var bytes = await TryGetL2Async(key, ct);
        if (bytes is not null)
        {
            var fromRedis = JsonSerializer.Deserialize<T>(bytes, Json);
            if (fromRedis is not null)
            {
                metrics.Hit("L2", policy.Name);
                logger.LogDebug("Cache hit L2 {CacheKey} ({SizeBytes} bytes), L1 refilled for {L1Ttl}", key, bytes.Length, policy.L1Absolute);
                SetL1(key, fromRedis, bytes.Length, policy);                     // зворотний шлях: L2 → L1
                return fromRedis;
            }
        }
        metrics.Miss("L2", policy.Name);
        logger.LogDebug("Cache miss {CacheKey} in L1 and L2, loading from database", key);

        // ---- джерело: БД ----
        var value = await factory(ct);
        var serialized = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        await TrySetL2Async(key, serialized, policy, ct);                          // зворотний шлях: БД → L2 → L1
        SetL1(key, value, serialized.Length, policy);
        logger.LogDebug("Cached {CacheKey}: {SizeBytes} bytes, L1 {L1Ttl}, L2 {L2Ttl}", key, serialized.Length, policy.L1Absolute, policy.L2Absolute);
        return value;
    }

    public async Task InvalidateAsync(string reason, CancellationToken ct, params string[] keys)
    {
        foreach (var key in keys)
        {
            memory.Remove(key);
            if (!L2Available) continue;                          // запис у Redis сам зникне за TTL
            try
            {
                await distributed.RemoveAsync(key, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // L2 недоступний: запис у Redis сам зникне за TTL; інші екземпляри мають короткий L1
                TripL2("invalidate", key, ex);
            }
        }
        metrics.Invalidated(keys.Length, reason);
        logger.LogInformation("Cache invalidated {CacheKeys}, reason {Reason}", keys, reason);
    }

    private void SetL1<T>(string key, T value, int sizeBytes, CachePolicy policy) =>
        memory.Set(key, value, new MemoryCacheEntryOptions
        {
            Size = Math.Max(1, sizeBytes / 1024),                     // «одиниці» SizeLimit ≈ КБ серіалізованого DTO
            SlidingExpiration = policy.L1Sliding,
            AbsoluteExpirationRelativeToNow = policy.L1Absolute,
            Priority = policy == CachePolicy.Reference ? CacheItemPriority.High : CacheItemPriority.Normal
        });

    private async Task<byte[]?> TryGetL2Async(string key, CancellationToken ct)
    {
        if (!L2Available) return null;
        try
        {
            return await distributed.GetAsync(key, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TripL2("get", key, ex);
            return null;
        }
    }

    private async Task TrySetL2Async(string key, byte[] value, CachePolicy policy, CancellationToken ct)
    {
        if (!L2Available) return;
        try
        {
            await distributed.SetAsync(key, value,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = policy.L2Absolute }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TripL2("set", key, ex);
        }
    }
}
