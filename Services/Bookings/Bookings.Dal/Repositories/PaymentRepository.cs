using Bookings.Dal.Infrastructure;
using Bookings.Dal.Interfaces;
using Bookings.Domain.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bookings.Dal.Repositories;

/// <summary>Оплати — ADO.NET + Dapper. Фінансові записи не видаляються (hasSoftDelete: false, Delete не викликається з BLL).</summary>
public sealed class PaymentRepository : BaseDapperRepository<Payment>, IPaymentRepository
{
    public PaymentRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(connection, currentTransaction, "dbo.Payments", hasSoftDelete: false) { }

    protected override string InsertSql => @"
        INSERT INTO dbo.Payments (BookingId, Amount, Method, Status, ProviderTxnId, PaidAt, CreatedBy)
        OUTPUT INSERTED.Id
        VALUES (@BookingId, @Amount, @Method, @Status, @ProviderTxnId, @PaidAt, @CreatedBy)";

    protected override string UpdateSql => @"
        UPDATE dbo.Payments
        SET Status = @Status, PaidAt = @PaidAt, UpdatedAt = SYSUTCDATETIME()
        WHERE Id = @Id";

    protected override string DefaultOrderBy => "CreatedAt DESC, Id DESC";

    public Task<IReadOnlyList<Payment>> GetByBookingAsync(long bookingId, CancellationToken ct = default) =>
        SqlErrorMapper.RunAsync<IReadOnlyList<Payment>>(async () =>
        {
            // IX_Payments_Booking_Status з Лаб. 01
            const string sql = @"
                SELECT Id, BookingId, Amount, Method, Status, ProviderTxnId, PaidAt, CreatedAt, CreatedBy
                FROM dbo.Payments WHERE BookingId = @BookingId ORDER BY CreatedAt";
            await EnsureOpenAsync(ct);
            var rows = await Connection.QueryAsync<Payment>(Command(sql, new { BookingId = bookingId }, ct));
            return rows.AsList();
        });

    public Task<Payment?> GetByProviderTxnIdAsync(string providerTxnId, CancellationToken ct = default) =>
        SqlErrorMapper.RunAsync(async () =>
        {
            // UX_Payments_ProviderTxnId з Лаб. 01
            const string sql = @"
                SELECT Id, BookingId, Amount, Method, Status, ProviderTxnId, PaidAt, CreatedAt, CreatedBy
                FROM dbo.Payments WHERE ProviderTxnId = @ProviderTxnId";
            await EnsureOpenAsync(ct);
            return await Connection.QuerySingleOrDefaultAsync<Payment>(
                Command(sql, new { ProviderTxnId = providerTxnId }, ct));
        });
}
