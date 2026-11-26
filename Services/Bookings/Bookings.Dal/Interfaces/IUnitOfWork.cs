using System.Data;

namespace Bookings.Dal.Interfaces;

/// <summary>
/// Unit of Work: ОДНЕ з'єднання на HTTP-запит (Scoped) і одна поточна транзакція,
/// яку бачать усі репозиторії. Аналог IUnitOfWork з eShop (Ordering.Domain/SeedWork),
/// реалізований вручну на ADO.NET.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    ICustomerRepository Customers { get; }
    IBookingRepository Bookings { get; }
    IShowtimeRepository Showtimes { get; }
    IPaymentRepository Payments { get; }

    bool HasActiveTransaction { get; }

    Task BeginTransactionAsync(IsolationLevel level = IsolationLevel.ReadCommitted, CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
