using System.Diagnostics.Metrics;

namespace Catalog.Bll.Caching;

/// <summary>
/// Лічильники кешу каталогу: cache.hits / cache.misses з тегом cache.level (L1 | L2) і cache.name,
/// cache.invalidations з причиною. Meter "Cinema.Cache" підписаний в ServiceDefaults (AddMeter),
/// тож метрики видно в Aspire Dashboard → Metrics → catalog-api.
/// </summary>
public sealed class CatalogCacheMetrics
{
    public const string MeterName = "Cinema.Cache";

    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;
    private readonly Counter<long> _invalidations;

    public CatalogCacheMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _hits = meter.CreateCounter<long>("cache.hits", unit: "{hit}", description: "Cache hits by level");
        _misses = meter.CreateCounter<long>("cache.misses", unit: "{miss}", description: "Cache misses by level");
        _invalidations = meter.CreateCounter<long>("cache.invalidations", unit: "{key}", description: "Invalidated cache keys");
    }

    public void Hit(string level, string name) =>
        _hits.Add(1, new KeyValuePair<string, object?>("cache.level", level), new KeyValuePair<string, object?>("cache.name", name));

    public void Miss(string level, string name) =>
        _misses.Add(1, new KeyValuePair<string, object?>("cache.level", level), new KeyValuePair<string, object?>("cache.name", name));

    public void Invalidated(int keys, string reason) =>
        _invalidations.Add(keys, new KeyValuePair<string, object?>("cache.reason", reason));
}
