using System.Diagnostics.Metrics;

namespace Reviews.WebApi.Metrics;

/// <summary>
/// Бізнес-метрики відгуків (Singleton, Meter "Reviews.Api" через IMeterFactory, AddMeter — у ServiceDefaults).
///
/// | Інструмент                   | Тип              | Теги                              |
/// |------------------------------|------------------|-----------------------------------|
/// | reviews.created              | Counter          | review.type, review.rating (1–5)  |
/// | reviews.updated              | Counter          | —                                 |
/// | reviews.deleted              | Counter          | —                                 |
/// | reviews.comments.added       | Counter          | —                                 |
/// | reviews.operation.duration   | Histogram (ms)   | operation (назва команди/запиту), status |
/// | reviews.requests.in_flight   | ObservableGauge  | —                                 |
/// Кардинальність низька: тип (3 значення), оцінка (5), назви команд/запитів (≈ 10), статус (2).
/// </summary>
public sealed class ReviewMetrics
{
    public const string MeterName = "Reviews.Api";

    private readonly Counter<long> _created;
    private readonly Counter<long> _updated;
    private readonly Counter<long> _deleted;
    private readonly Counter<long> _comments;
    private readonly Histogram<double> _duration;
    private int _inFlight;

    public ReviewMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _created = meter.CreateCounter<long>("reviews.created", unit: "{review}", description: "Created reviews");
        _updated = meter.CreateCounter<long>("reviews.updated", unit: "{review}", description: "Edited reviews");
        _deleted = meter.CreateCounter<long>("reviews.deleted", unit: "{review}", description: "Deleted reviews");
        _comments = meter.CreateCounter<long>("reviews.comments.added", unit: "{comment}", description: "Added comments");
        _duration = meter.CreateHistogram<double>("reviews.operation.duration", unit: "ms",
            description: "Duration of MediatR commands and queries (successful and failed)");
        meter.CreateObservableGauge("reviews.requests.in_flight", () => Volatile.Read(ref _inFlight),
            unit: "{request}", description: "Commands and queries being handled right now");
    }

    public void ReviewCreated(string type, int rating) =>
        _created.Add(1, new KeyValuePair<string, object?>("review.type", type), new KeyValuePair<string, object?>("review.rating", rating));
    public void ReviewUpdated() => _updated.Add(1);
    public void ReviewDeleted() => _deleted.Add(1);
    public void CommentAdded() => _comments.Add(1);

    public void RecordDuration(string operation, bool success, double elapsedMs) =>
        _duration.Record(elapsedMs,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("status", success ? "success" : "failure"));

    public void Enter() => Interlocked.Increment(ref _inFlight);
    public void Exit() => Interlocked.Decrement(ref _inFlight);
}
