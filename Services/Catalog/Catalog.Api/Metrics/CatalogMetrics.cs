using System.Diagnostics.Metrics;
using Microsoft.Extensions.Caching.Memory;

namespace Catalog.Api.Metrics;

/// <summary>
/// Бізнес-метрики каталогу (Singleton, Meter "Catalog.Api" через IMeterFactory, AddMeter — у ServiceDefaults).
///
/// | Інструмент                     | Тип              | Теги                    |
/// |--------------------------------|------------------|-------------------------|
/// | movies.created                 | Counter          | movie.age_rating        |
/// | movies.updated                 | Counter          | change (price, activated, deactivated, genres, deleted) |
/// | showtimes.scheduled            | Counter          | showtime.language       |
/// | showtimes.cancelled            | Counter          | —                       |
/// | catalog.operation.duration     | Histogram (ms)   | operation, status       |
/// | catalog.cache.l1.entries       | ObservableGauge  | —                       |
/// | catalog.cache.l1.size          | ObservableGauge  | —  (одиниці SizeLimit ≈ КБ) |
/// Жодних тегів з Id фільмів чи сеансів — лише кілька фіксованих значень.
/// </summary>
public sealed class CatalogMetrics
{
    public const string MeterName = "Catalog.Api";

    private readonly Counter<long> _moviesCreated;
    private readonly Counter<long> _moviesUpdated;
    private readonly Counter<long> _showtimesScheduled;
    private readonly Counter<long> _showtimesCancelled;
    private readonly Histogram<double> _duration;

    public CatalogMetrics(IMeterFactory meterFactory, IMemoryCache memoryCache)
    {
        var meter = meterFactory.Create(MeterName);
        _moviesCreated = meter.CreateCounter<long>("movies.created", unit: "{movie}", description: "Created movies");
        _moviesUpdated = meter.CreateCounter<long>("movies.updated", unit: "{change}", description: "Movie changes by kind");
        _showtimesScheduled = meter.CreateCounter<long>("showtimes.scheduled", unit: "{showtime}", description: "Scheduled showtimes");
        _showtimesCancelled = meter.CreateCounter<long>("showtimes.cancelled", unit: "{showtime}", description: "Cancelled showtimes");
        _duration = meter.CreateHistogram<double>("catalog.operation.duration", unit: "ms",
            description: "Duration of catalog operations (successful and failed)");

        // gauge: поточний стан L1-кешу (MemoryCache з TrackStatistics = true)
        var cache = memoryCache as MemoryCache;
        meter.CreateObservableGauge("catalog.cache.l1.entries", () => cache?.GetCurrentStatistics()?.CurrentEntryCount ?? 0,
            unit: "{entry}", description: "Entries in the in-memory (L1) cache");
        meter.CreateObservableGauge("catalog.cache.l1.size", () => cache?.GetCurrentStatistics()?.CurrentEstimatedSize ?? 0,
            unit: "{unit}", description: "Estimated L1 cache size in SizeLimit units (~KB)");
    }

    public void MovieCreated(string ageRating) => _moviesCreated.Add(1, new KeyValuePair<string, object?>("movie.age_rating", ageRating));
    public void MovieUpdated(string change) => _moviesUpdated.Add(1, new KeyValuePair<string, object?>("change", change));
    public void ShowtimeScheduled(string language) => _showtimesScheduled.Add(1, new KeyValuePair<string, object?>("showtime.language", language));
    public void ShowtimeCancelled() => _showtimesCancelled.Add(1);

    public void RecordDuration(string operation, bool success, double elapsedMs) =>
        _duration.Record(elapsedMs,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("status", success ? "success" : "failure"));
}
