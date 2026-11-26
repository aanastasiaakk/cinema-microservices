using System.Data;
using Bookings.Dal.Infrastructure;
using Bookings.Dal.Interfaces;
using Bookings.Domain.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bookings.Dal.Repositories;

/// <summary>
/// Бронювання — ADO.NET + Dapper. Спільний CRUD успадковано з BaseDapperRepository,
/// тут — лише специфічне: multi-mapping (бронювання + деталі + квитки одним запитом),
/// виклики збережених процедур Лаб. 01, робота з квитками.
/// </summary>
public sealed class BookingRepository : BaseDapperRepository<Booking>, IBookingRepository
{
    public BookingRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(connection, currentTransaction, "dbo.Bookings", hasSoftDelete: true) { }

    protected override string InsertSql => @"
        INSERT INTO dbo.Bookings (CustomerId, BookingCode, Status, TotalAmount, CreatedBy)
        OUTPUT INSERTED.Id
        VALUES (@CustomerId, @BookingCode, @Status, @TotalAmount, @CreatedBy)";

    protected override string UpdateSql => @"
        UPDATE dbo.Bookings
        SET TotalAmount = @TotalAmount, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UpdatedBy
        WHERE Id = @Id AND IsDeleted = 0 AND RowVersion = @RowVersion";

    protected override string DefaultOrderBy => "BookedAt DESC, Id DESC";

    public Task<Booking?> GetWithTicketsAsync(long bookingId, CancellationToken ct = default) =>
        SqlErrorMapper.RunAsync(async () =>
        {
            // Один запит: кожен рядок JOIN = Booking + BookingDetails + один Ticket.
            const string sql = @"
                SELECT b.Id, b.CustomerId, b.BookingCode, b.Status, b.TotalAmount, b.BookedAt,
                       b.CreatedAt, b.CreatedBy, b.UpdatedAt, b.UpdatedBy, b.IsDeleted, b.RowVersion,
                       d.BookingId, d.Channel, d.ContactPhone, d.Comment, d.CancelReason,
                       t.BookingId, t.ShowtimeId, t.SeatRow, t.SeatNumber, t.TicketType,
                       t.MovieTitle, t.StartsAt, t.UnitPrice
                FROM dbo.Bookings b
                LEFT JOIN dbo.BookingDetails d ON d.BookingId = b.Id
                LEFT JOIN dbo.Tickets t        ON t.BookingId = b.Id
                WHERE b.Id = @BookingId AND b.IsDeleted = 0
                ORDER BY t.StartsAt, t.SeatRow, t.SeatNumber";

            await EnsureOpenAsync(ct);
            Booking? result = null;
            await Connection.QueryAsync<Booking, BookingDetails?, Ticket?, Booking>(
                Command(sql, new { BookingId = bookingId }, ct),
                (booking, details, ticket) =>
                {
                    result ??= booking;
                    result.Details ??= details;
                    if (ticket is not null) result.Tickets.Add(ticket);
                    return result;
                },
                splitOn: "BookingId,BookingId");   // з цих колонок починаються BookingDetails і Ticket
            return result;
        });

    public Task<IReadOnlyList<Booking>> GetByCustomerAsync(
        long customerId, string? status, int page, int pageSize, CancellationToken ct = default) =>
        SqlErrorMapper.RunAsync<IReadOnlyList<Booking>>(async () =>
        {
            // Використовує IX_Bookings_Customer_Status_Active з Лаб. 01
            const string sql = @"
                SELECT Id, CustomerId, BookingCode, Status, TotalAmount, BookedAt,
                       CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted, RowVersion
                FROM dbo.Bookings
                WHERE CustomerId = @CustomerId AND IsDeleted = 0
                  AND (@Status IS NULL OR Status = @Status)
                ORDER BY BookedAt DESC, Id DESC
                OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";
            await EnsureOpenAsync(ct);
            var rows = await Connection.QueryAsync<Booking>(Command(sql,
                new { CustomerId = customerId, Status = status, Skip = (page - 1) * pageSize, Take = pageSize }, ct));
            return rows.AsList();
        });

    public Task AddDetailsAsync(BookingDetails details, CancellationToken ct = default) =>
        SqlErrorMapper.RunAsync(async () =>
        {
            const string sql = @"
                INSERT INTO dbo.BookingDetails (BookingId, Channel, ContactPhone, Comment)
                VALUES (@BookingId, @Channel, @ContactPhone, @Comment)";
            await EnsureOpenAsync(ct);
            await Connection.ExecuteAsync(Command(sql, details, ct));
        });

    public Task<bool> IsSeatTakenAsync(long showtimeId, byte row, byte seat, CancellationToken ct = default) =>
        SqlErrorMapper.RunAsync(async () =>
        {
            // UPDLOCK + HOLDLOCK: діапазон блокується до кінця транзакції UoW —
            // два паралельні запити не продадуть одне місце (pessimistic concurrency).
            const string sql = @"
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM dbo.Tickets t WITH (UPDLOCK, HOLDLOCK)
                    JOIN dbo.Bookings b ON b.Id = t.BookingId
                    WHERE t.ShowtimeId = @ShowtimeId AND t.SeatRow = @SeatRow AND t.SeatNumber = @SeatNumber
                      AND b.Status <> N'Cancelled' AND b.IsDeleted = 0) THEN 1 ELSE 0 END";
            await EnsureOpenAsync(ct);
            return await Connection.ExecuteScalarAsync<int>(Command(sql,
                new { ShowtimeId = showtimeId, SeatRow = row, SeatNumber = seat }, ct)) == 1;
        });

    public Task AddTicketAsync(Ticket ticket, CancellationToken ct = default) =>
        SqlErrorMapper.RunAsync(async () =>
        {
            const string sql = @"
                INSERT INTO dbo.Tickets (BookingId, ShowtimeId, SeatRow, SeatNumber, TicketType,
                                         MovieTitle, StartsAt, UnitPrice)
                VALUES (@BookingId, @ShowtimeId, @SeatRow, @SeatNumber, @TicketType,
                        @MovieTitle, @StartsAt, @UnitPrice)";
            await EnsureOpenAsync(ct);
            await Connection.ExecuteAsync(Command(sql, ticket, ct));
        });

    public Task<decimal> RecalculateTotalAsync(long bookingId, string user, CancellationToken ct = default) =>
        SqlErrorMapper.RunAsync(async () =>
        {
            const string sql = @"
                UPDATE dbo.Bookings
                SET TotalAmount = ISNULL((SELECT SUM(UnitPrice) FROM dbo.Tickets WHERE BookingId = @Id), 0),
                    UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @User
                OUTPUT INSERTED.TotalAmount
                WHERE Id = @Id";
            await EnsureOpenAsync(ct);
            return await Connection.ExecuteScalarAsync<decimal>(Command(sql, new { Id = bookingId, User = user }, ct));
        });

    public Task ChangeStatusAsync(long bookingId, string newStatus, string? reason,
                                  byte[]? expectedRowVersion, string user, CancellationToken ct = default) =>
        SqlErrorMapper.RunAsync(async () =>
        {
            // Процедура з Лаб. 01: перевіряє допустимість переходу і кидає THROW 50002
            await EnsureOpenAsync(ct);
            var parameters = new DynamicParameters();
            parameters.Add("@BookingId", bookingId, DbType.Int64);
            parameters.Add("@NewStatus", newStatus, DbType.String, size: 30);
            parameters.Add("@Reason", reason, DbType.String, size: 300);
            parameters.Add("@ExpectedRowVersion", expectedRowVersion, DbType.Binary, size: 8);
            parameters.Add("@User", user, DbType.String, size: 100);
            await Connection.ExecuteAsync(Command("dbo.usp_Booking_ChangeStatus", parameters, ct,
                CommandType.StoredProcedure));
        });

    public Task SoftDeleteAsync(long bookingId, string user, CancellationToken ct = default) =>
        SqlErrorMapper.RunAsync(async () =>
        {
            await EnsureOpenAsync(ct);
            await Connection.ExecuteAsync(Command("dbo.usp_Booking_SoftDelete",
                new { BookingId = bookingId, User = user }, ct, CommandType.StoredProcedure));
        });
}
