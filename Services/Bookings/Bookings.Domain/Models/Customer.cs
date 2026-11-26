namespace Bookings.Domain.Models;

/// <summary>Клієнт кінотеатру (таблиця dbo.Customers). Власник даних — контекст Booking.</summary>
public class Customer
{
    public long Id { get; set; }
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    /// <summary>rowversion для оптимістичної конкурентності.</summary>
    public byte[] RowVersion { get; set; } = [];
}
