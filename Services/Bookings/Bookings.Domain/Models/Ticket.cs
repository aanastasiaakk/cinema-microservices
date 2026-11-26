namespace Bookings.Domain.Models;

/// <summary>
/// Квиток (dbo.Tickets) — проміжна таблиця M:N Bookings ↔ Showtimes.
/// MovieTitle, StartsAt, UnitPrice — ЗНІМОК на момент бронювання (політика дублювання Лаб. 01),
/// після створення не оновлюються (як OrderItem.ProductName/UnitPrice в eShop).
/// </summary>
public class Ticket
{
    public long BookingId { get; set; }
    public long ShowtimeId { get; set; }
    public byte SeatRow { get; set; }
    public byte SeatNumber { get; set; }
    public string TicketType { get; set; } = TicketTypes.Adult;
    public string MovieTitle { get; set; } = "";
    public DateTime StartsAt { get; set; }
    public decimal UnitPrice { get; set; }
}
