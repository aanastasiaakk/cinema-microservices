using System.Diagnostics.Metrics;

namespace Platform.Aggregator.Metrics;

/// <summary>
/// Бізнес-метрики Aggregator (Meter "Platform.Aggregator", підключено в ServiceDefaults):
///  • aggregator.cards — зібрані картки; теги card.result (full | partial | not_found | unavailable),
///    transport (grpc | http), cache (hit | miss | bypass);
///  • aggregator.card.duration — тривалість УСІХ запитів картки, зокрема невдалих;
///  • aggregator.requests.in_flight — gauge: скільки карток збирається просто зараз.
/// Теги мають кілька фіксованих значень — жодних Id бронювань чи користувачів (низька кардинальність).
/// </summary>
public sealed class AggregatorMetrics
{
    public const string MeterName = "Platform.Aggregator";

    private readonly Counter<long> _cards;
    private readonly Histogram<double> _duration;
    private int _inFlight;

    public AggregatorMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _cards = meter.CreateCounter<long>("aggregator.cards", unit: "{card}", description: "Booking cards served by the aggregator");
        _duration = meter.CreateHistogram<double>("aggregator.card.duration", unit: "ms", description: "Duration of booking card requests");
        meter.CreateObservableGauge("aggregator.requests.in_flight", () => Volatile.Read(ref _inFlight),
            unit: "{request}", description: "Booking cards being aggregated right now");
    }

    public IDisposable Track() { Interlocked.Increment(ref _inFlight); return new Releaser(this); }

    public void CardServed(string result, string transport, string cache, double elapsedMs)
    {
        KeyValuePair<string, object?>[] tags = [new("card.result", result), new("transport", transport), new("cache", cache)];
        _cards.Add(1, tags);
        _duration.Record(elapsedMs, tags);
    }

    private sealed class Releaser(AggregatorMetrics metrics) : IDisposable
    {
        public void Dispose() => Interlocked.Decrement(ref metrics._inFlight);
    }
}
