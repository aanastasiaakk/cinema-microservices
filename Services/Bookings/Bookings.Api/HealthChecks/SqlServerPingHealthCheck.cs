using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bookings.Api.HealthChecks;

/// <summary>
/// Легка перевірка SQL Server: відкрити з'єднання і виконати SELECT 1 (без звернень до таблиць).
/// Таймаут задає реєстрація (5 с); збій → failureStatus реєстрації (Unhealthy — без БД сервіс не працює).
/// </summary>
public sealed class SqlServerPingHealthCheck(string connectionString) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            // короткий таймаут з'єднання: health check має відповідати за секунди, а не чекати типові 15 с
            var builder = new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = 3 };
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync(ct);
            await using var command = new SqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(ct);
            return HealthCheckResult.Healthy($"SQL Server '{connection.DataSource}', database '{connection.Database}' is reachable");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "SQL Server is unreachable", ex);
        }
    }
}

public static class BookingHealthChecks
{
    /// <summary>booking-db (SQL Server, теги database/sql/ready, 5 с) + стандартний gRPC Health Checking Protocol.</summary>
    public static IServiceCollection AddBookingHealthChecks(this IServiceCollection services, string connectionString)
    {
        services.AddHealthChecks()
            .Add(new HealthCheckRegistration("booking-db", _ => new SqlServerPingHealthCheck(connectionString),
                HealthStatus.Unhealthy, ["database", "sql", Microsoft.Extensions.Hosting.Extensions.ReadyTag], TimeSpan.FromSeconds(5)));
        // grpc.health.v1.Health/Check відповідає на основі тих самих перевірок (Aggregator опитує його)
        services.AddGrpcHealthChecks(o => o.Services.Map("", r => r.Tags.Contains(Microsoft.Extensions.Hosting.Extensions.ReadyTag)));
        return services;
    }
}
