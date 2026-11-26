using Bookings.Dal.Infrastructure;
using Bookings.Dal.Interfaces;
using Bookings.Domain.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bookings.Dal.Repositories;

/// <summary>
/// Локальна копія сеансів з Каталогу — ADO.NET + Dapper.
/// Записи (Add/Update з бази) виконуватиме обробник подій ShowtimeScheduled/Changed (Лаб. 08);
/// API сервісу Booking сеанси лише читає.
/// </summary>
public sealed class ShowtimeRepository : BaseDapperRepository<Showtime>, IShowtimeRepository
{
    public ShowtimeRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(connection, currentTransaction, "dbo.Showtimes", hasSoftDelete: false) { }

    // Id не IDENTITY — це Id з CinemaCatalog
    protected override string InsertSql => @"
        INSERT INTO dbo.Showtimes (Id, MovieId, MovieTitle, HallName, StartsAt, BasePrice, Capacity)
        OUTPUT INSERTED.Id
        VALUES (@Id, @MovieId, @MovieTitle, @HallName, @StartsAt, @BasePrice, @Capacity)";

    protected override string UpdateSql => @"
        UPDATE dbo.Showtimes
        SET MovieTitle = @MovieTitle, HallName = @HallName, StartsAt = @StartsAt,
            BasePrice = @BasePrice, Capacity = @Capacity, SyncedAt = SYSUTCDATETIME()
        WHERE Id = @Id";

    protected override string DefaultOrderBy => "StartsAt, Id";

    public Task<IReadOnlyList<Showtime>> GetUpcomingAsync(DateTime from, DateTime to, CancellationToken ct = default) =>
        SqlErrorMapper.RunAsync<IReadOnlyList<Showtime>>(async () =>
        {
            // IX_Showtimes_StartsAt з Лаб. 01
            const string sql = @"
                SELECT Id, MovieId, MovieTitle, HallName, StartsAt, BasePrice, Capacity, SyncedAt
                FROM dbo.Showtimes
                WHERE StartsAt >= @From AND StartsAt < @To
                ORDER BY StartsAt";
            await EnsureOpenAsync(ct);
            var rows = await Connection.QueryAsync<Showtime>(Command(sql, new { From = from, To = to }, ct));
            return rows.AsList();
        });

    public Task<IReadOnlyList<(byte Row, byte Seat)>> GetTakenSeatsAsync(long showtimeId, CancellationToken ct = default) =>
        SqlErrorMapper.RunAsync<IReadOnlyList<(byte Row, byte Seat)>>(async () =>
        {
            // IX_Tickets_Showtime_Seat з Лаб. 01
            const string sql = @"
                SELECT t.SeatRow, t.SeatNumber
                FROM dbo.Tickets t
                JOIN dbo.Bookings b ON b.Id = t.BookingId
                WHERE t.ShowtimeId = @ShowtimeId AND b.Status <> N'Cancelled' AND b.IsDeleted = 0
                ORDER BY t.SeatRow, t.SeatNumber";
            await EnsureOpenAsync(ct);
            var rows = await Connection.QueryAsync<(byte, byte)>(Command(sql, new { ShowtimeId = showtimeId }, ct));
            return rows.AsList();
        });
}
