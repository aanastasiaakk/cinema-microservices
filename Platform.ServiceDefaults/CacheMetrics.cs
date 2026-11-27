using System.Diagnostics.Metrics;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Метрики кешу для всіх сервісів платформи: cache.hits / cache.misses / cache.invalidations
/// з тегами cache.level (L1 = пам'ять, L2 = Redis) і cache.name. Meter "Cinema.Cache" підключено в
/// ServiceDefaults (WithMetrics → AddMeter), тож лічильники видно у вкладці Metrics Aspire Dashboard.
/// Частка попадань (hit ratio) = hits / (hits + misses) для кожного рівня.
/// </summary>
public sealed class CacheMetrics
{
    public const string MeterName = "Cinema.Cache";
    public const string L1 = "L1";
    public const string L2 = "L2";

    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;
    private readonly Counter<long> _invalidations;
    private readonly Histogram<double> _l2Duration;

    public CacheMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _hits = meter.CreateCounter<long>("cache.hits", unit: "{hit}", description: "Cache hits by level");
        _misses = meter.CreateCounter<long>("cache.misses", unit: "{miss}", description: "Cache misses by level");
        _invalidations = meter.CreateCounter<long>("cache.invalidations", unit: "{key}", description: "Removed cache keys");
        _l2Duration = meter.CreateHistogram<double>("cache.l2.duration", unit: "ms", description: "Redis (L2) operation duration");
    }

    public void Hit(string level, string cacheName) => _hits.Add(1, Tags(level, cacheName));
    public void Miss(string level, string cacheName) => _misses.Add(1, Tags(level, cacheName));
    public void Invalidated(string cacheName, int keys = 1) => _invalidations.Add(keys, new KeyValuePair<string, object?>("cache.name", cacheName));
    public void L2Duration(double ms, string operation) => _l2Duration.Record(ms, new KeyValuePair<string, object?>("cache.operation", operation));

    private static KeyValuePair<string, object?>[] Tags(string level, string cacheName) =>
        [new("cache.level", level), new("cache.name", cacheName)];
}
