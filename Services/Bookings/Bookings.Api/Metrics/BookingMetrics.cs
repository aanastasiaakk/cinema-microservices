using System.Diagnostics.Metrics;

namespace Bookings.Api.Metrics;

/// <summary>
/// Бізнес-метрики бронювань. Singleton, Meter "Bookings.Api" створено через IMeterFactory і підключено в
/// ServiceDefaults (AddMeter). Теги мають кілька фіксованих значень (канал, статус, спосіб оплати, операція,
/// результат) — жодних Id бронювань чи клієнтів, тож кількість часових рядів обмежена.
///
/// | Інструмент                    | Тип              | Теги                         |
/// |-------------------------------|------------------|------------------------------|
/// | bookings.created              | Counter          | booking.channel              |
/// | bookings.status_changed       | Counter          | booking.status               |
/// | bookings.paid                 | Counter          | payment.method               |
/// | bookings.revenue              | Counter (UAH)    | payment.method               |
/// | bookings.operation.duration   | Histogram (ms)   | operation, status            |
/// | bookings.current              | ObservableGauge  | booking.status               |
/// </summary>
public sealed class BookingMetrics
{
    public const string MeterName = "Bookings.Api";

    private readonly Counter<long> _created;
    private readonly Counter<long> _statusChanged;
    private readonly Counter<long> _paid;
    private readonly Counter<double> _revenue;
    private readonly Histogram<double> _duration;
    private volatile IReadOnlyDictionary<string, int> _snapshot = new Dictionary<string, int>();

    public BookingMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _created = meter.CreateCounter<long>("bookings.created", unit: "{booking}", description: "Created bookings");
        _statusChanged = meter.CreateCounter<long>("bookings.status_changed", unit: "{booking}", description: "Booking status transitions");
        _paid = meter.CreateCounter<long>("bookings.paid", unit: "{booking}", description: "Paid bookings");
        _revenue = meter.CreateCounter<double>("bookings.revenue", unit: "UAH", description: "Paid amount");
        _duration = meter.CreateHistogram<double>("bookings.operation.duration", unit: "ms",
            description: "Duration of booking operations (successful and failed)");
        // gauge: знімок поточного стану, значення читається в момент збору метрик
        meter.CreateObservableGauge("bookings.current", ObserveCurrent, unit: "{booking}",
            description: "Current number of bookings per status");
    }

    public void BookingCreated(string channel) => _created.Add(1, new KeyValuePair<string, object?>("booking.channel", channel));

    public void StatusChanged(string status) => _statusChanged.Add(1, new KeyValuePair<string, object?>("booking.status", status));

    public void BookingPaid(string method, decimal amount)
    {
        var tag = new KeyValuePair<string, object?>("payment.method", method);
        _paid.Add(1, tag);
        _revenue.Add((double)amount, tag);
    }

    public void RecordDuration(string operation, bool success, double elapsedMs) =>
        _duration.Record(elapsedMs,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("status", success ? "success" : "failure"));

    /// <summary>Оновлюється фоновим BookingsSnapshotService (запит до БД раз на 15 с, а не на кожен збір).</summary>
    public void UpdateSnapshot(IReadOnlyDictionary<string, int> countsByStatus) => _snapshot = countsByStatus;

    private IEnumerable<Measurement<int>> ObserveCurrent() =>
        _snapshot.Select(kv => new Measurement<int>(kv.Value, new KeyValuePair<string, object?>("booking.status", kv.Key)));
}
