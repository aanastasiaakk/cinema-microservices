namespace Bookings.Domain.Models;

/// <summary>Корінь агрегату «Бронювання» (dbo.Bookings + BookingDetails + Tickets).</summary>
public class Booking
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public string BookingCode { get; set; } = "";
    public string Status { get; set; } = BookingStatus.Created;
    public decimal TotalAmount { get; set; }
    public DateTime BookedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public byte[] RowVersion { get; set; } = [];

    /// <summary>1:1 — деталі бронювання.</summary>
    public BookingDetails? Details { get; set; }

    /// <summary>M:N з сеансами через квитки.</summary>
    public List<Ticket> Tickets { get; set; } = [];
}

/// <summary>dbo.BookingDetails — 1:1 з Bookings (FK = PK).</summary>
public class BookingDetails
{
    public long BookingId { get; set; }
    public string Channel { get; set; } = "Web";
    public string? ContactPhone { get; set; }
    public string? Comment { get; set; }
    public string? CancelReason { get; set; }
}
