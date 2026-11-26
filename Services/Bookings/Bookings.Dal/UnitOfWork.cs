using System.Data;
using Bookings.Dal.Interfaces;
using Bookings.Dal.Repositories;
using Microsoft.Data.SqlClient;

namespace Bookings.Dal;

/// <summary>
/// Unit of Work на ADO.NET.
/// • Одне SqlConnection на весь життєвий цикл (Scoped = один HTTP-запит); з'єднання береться з пулу
///   SqlClient і повертається туди в DisposeAsync.
/// • Одна поточна SqlTransaction, яку всі репозиторії отримують через делегат () => _transaction,
///   тож кілька репозиторіїв гарантовано працюють в одній транзакції.
///
/// Рівень ізоляції за замовчуванням — ReadCommitted (за замовчуванням SQL Server):
///   вищі рівні (RepeatableRead, Serializable) прибирають аномалії читання, але тримають більше
///   блокувань довше і різко підвищують ризик взаємних блокувань (deadlocks).
///   Тому: ReadCommitted + точкові блокування там, де потрібна гарантія
///   (UPDLOCK, HOLDLOCK при перевірці місця; UPDLOCK у процедурі зміни статусу).
/// Таймаути: з'єднання — 15 с (Connect Timeout у рядку підключення), команди — 30 с (RepositoryBase).
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly SqlConnection _connection;
    private SqlTransaction? _transaction;

    public UnitOfWork(string connectionString)
    {
        _connection = new SqlConnection(connectionString);

        // усі репозиторії отримують ОДНЕ з'єднання і доступ до ПОТОЧНОЇ транзакції
        Customers = new CustomerRepository(_connection, () => _transaction);
        Bookings = new BookingRepository(_connection, () => _transaction);
        Showtimes = new ShowtimeRepository(_connection, () => _transaction);
        Payments = new PaymentRepository(_connection, () => _transaction);
    }

    public ICustomerRepository Customers { get; }
    public IBookingRepository Bookings { get; }
    public IShowtimeRepository Showtimes { get; }
    public IPaymentRepository Payments { get; }

    public bool HasActiveTransaction => _transaction is not null;

    public async Task BeginTransactionAsync(
        IsolationLevel level = IsolationLevel.ReadCommitted, CancellationToken ct = default)
    {
        if (_transaction is not null)
            throw new InvalidOperationException("Transaction is already started");
        if (_connection.State != ConnectionState.Open)
            await _connection.OpenAsync(ct);
        _transaction = (SqlTransaction)await _connection.BeginTransactionAsync(level, ct);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("No active transaction");
        try
        {
            await _transaction.CommitAsync(ct);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction is null) return;
        try
        {
            // Якщо сервер уже відкотив транзакцію (XACT_ABORT / ROLLBACK у процедурі),
            // SqlTransaction стає «zombie» (Connection == null) — повторний Rollback не потрібен.
            if (_transaction.Connection is not null)
                await _transaction.RollbackAsync(CancellationToken.None);   // відкат не скасовуємо
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
            await _transaction.DisposeAsync();   // незавершена транзакція відкочується
        await _connection.DisposeAsync();         // з'єднання повертається в пул
    }
}
