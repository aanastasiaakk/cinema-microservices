using System.Data;
using Microsoft.Data.SqlClient;

namespace Bookings.Dal.Infrastructure;

/// <summary>
/// Спільна база всіх репозиторіїв: отримує НЕ рядок підключення, а спільне з'єднання
/// Unit of Work і доступ до його поточної транзакції. Так усі репозиторії однієї
/// бізнес-операції працюють в одному з'єднанні та одній транзакції.
/// </summary>
public abstract class RepositoryBase
{
    /// <summary>Таймаут команд (с). Довші операції — ознака проблеми, краще швидко отримати помилку.</summary>
    protected const int CommandTimeoutSeconds = 30;

    protected readonly SqlConnection Connection;
    private readonly Func<SqlTransaction?> _currentTransaction;

    protected RepositoryBase(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
    {
        Connection = connection;
        _currentTransaction = currentTransaction;
    }

    /// <summary>Поточна транзакція Unit of Work (або null, якщо її не розпочато).</summary>
    protected SqlTransaction? Transaction => _currentTransaction();

    protected async Task EnsureOpenAsync(CancellationToken ct)
    {
        if (Connection.State != ConnectionState.Open)
            await Connection.OpenAsync(ct);
    }
}
