namespace Bookings.Domain.Models;

/// <summary>
/// Локальна копія сеансу з Каталогу (dbo.Showtimes у CinemaBooking).
/// Id = Id у CinemaCatalog, MovieId — посилання без FK. Поля оновлюються подіями з Каталогу (Лаб. 08).
/// </summary>
public class Showtime
{
    public long Id { get; set; }
    public long MovieId { get; set; }
    public string MovieTitle { get; set; } = "";
    public string HallName { get; set; } = "";
    public DateTime StartsAt { get; set; }
    public decimal BasePrice { get; set; }
    public int Capacity { get; set; }
    public DateTime SyncedAt { get; set; }
}
