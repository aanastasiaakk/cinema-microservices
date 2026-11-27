using Microsoft.Data.SqlClient;

namespace Bookings.Api.Metrics;

/// <summary>
/// Раз на 15 с рахує бронювання за статусами (одним легким GROUP BY) для gauge bookings.current.
/// Збій БД лише логується — метрики не мають впливати на роботу сервісу.
/// </summary>
public sealed class BookingsSnapshotService(BookingMetrics metrics, IConfiguration configuration,
                                            ILogger<BookingsSnapshotService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = configuration.GetConnectionString("CinemaBooking");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(stoppingToken);
                await using var command = new SqlCommand(
                    "SELECT Status, COUNT(*) FROM dbo.Bookings WHERE IsDeleted = 0 GROUP BY Status", connection) { CommandTimeout = 5 };
                var counts = new Dictionary<string, int>();
                await using var reader = await command.ExecuteReaderAsync(stoppingToken);
                while (await reader.ReadAsync(stoppingToken)) counts[reader.GetString(0)] = reader.GetInt32(1);
                metrics.UpdateSnapshot(counts);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning("Bookings snapshot for metrics failed: {Error}", ex.Message);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
