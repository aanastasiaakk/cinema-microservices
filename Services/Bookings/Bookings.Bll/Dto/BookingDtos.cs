using System.ComponentModel.DataAnnotations;

namespace Bookings.Bll.Dto;

/// <summary>Квиток у відповіді. MovieTitle/StartsAt/UnitPrice — знімок на момент бронювання.</summary>
public sealed class TicketDto
{
    public long ShowtimeId { get; init; }
    public string MovieTitle { get; init; } = "";
    public DateTime StartsAt { get; init; }
    public byte SeatRow { get; init; }
    public byte SeatNumber { get; init; }
    public string TicketType { get; init; } = "";
    public decimal UnitPrice { get; init; }
}

/// <summary>Бронювання з деталями і квитками (вихід).</summary>
public sealed class BookingDto
{
    public long Id { get; init; }
    public long CustomerId { get; init; }
    public string BookingCode { get; init; } = "";
    public string Status { get; init; } = "";
    public decimal TotalAmount { get; init; }
    public DateTime BookedAt { get; init; }
    public string? Channel { get; init; }
    public string? Comment { get; init; }
    public string? CancelReason { get; init; }
    public string RowVersion { get; init; } = "";
    public IReadOnlyList<TicketDto> Tickets { get; init; } = [];
}

/// <summary>Рядок списку бронювань (вихід).</summary>
public sealed class BookingSummaryDto
{
    public long Id { get; init; }
    public string BookingCode { get; init; } = "";
    public string Status { get; init; } = "";
    public decimal TotalAmount { get; init; }
    public DateTime BookedAt { get; init; }
}

/// <summary>Квиток у запиті на бронювання.</summary>
public sealed class CreateTicketDto
{
    [Range(1, long.MaxValue)]
    public long ShowtimeId { get; set; }

    [Range(1, 50)]
    public byte SeatRow { get; set; }

    [Range(1, 60)]
    public byte SeatNumber { get; set; }

    [RegularExpression("^(Adult|Student|Child)$")]
    public string TicketType { get; set; } = "Adult";
}

/// <summary>Створення бронювання з квитками (вхід).</summary>
public sealed class CreateBookingDto
{
    [Range(1, long.MaxValue)]
    public long CustomerId { get; set; }

    [RegularExpression("^(Web|Mobile|BoxOffice)$")]
    public string Channel { get; set; } = "Web";

    [Phone, MaxLength(20)]
    public string? ContactPhone { get; set; }

    [MaxLength(500)]
    public string? Comment { get; set; }

    [Required, MinLength(1), MaxLength(10)]
    public List<CreateTicketDto> Tickets { get; set; } = [];
}

/// <summary>Скасування бронювання (вхід).</summary>
public sealed class CancelBookingDto
{
    [MaxLength(300)]
    public string? Reason { get; set; }
}
