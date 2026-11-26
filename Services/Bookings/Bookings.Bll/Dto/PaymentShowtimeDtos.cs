using System.ComponentModel.DataAnnotations;

namespace Bookings.Bll.Dto;

/// <summary>Оплата бронювання (вхід). Idempotency-ключ передається заголовком Idempotency-Key.</summary>
public sealed class PayBookingDto
{
    [Range(typeof(decimal), "0.01", "100000")]
    public decimal Amount { get; set; }

    [Required, RegularExpression("^(Card|Cash|ApplePay|GooglePay)$")]
    public string Method { get; set; } = "Card";
}

/// <summary>Оплата (вихід).</summary>
public sealed class PaymentDto
{
    public long Id { get; init; }
    public long BookingId { get; init; }
    public decimal Amount { get; init; }
    public string Method { get; init; } = "";
    public string Status { get; init; } = "";
    public string? ProviderTxnId { get; init; }
    public DateTime? PaidAt { get; init; }
}

/// <summary>Сеанс з локальної копії довідника Каталогу (вихід).</summary>
public sealed class ShowtimeDto
{
    public long Id { get; init; }
    public long MovieId { get; init; }
    public string MovieTitle { get; init; } = "";
    public string HallName { get; init; } = "";
    public DateTime StartsAt { get; init; }
    public decimal BasePrice { get; init; }
    public int Capacity { get; init; }
}

/// <summary>Зайняті місця сеансу (вихід).</summary>
public sealed class ShowtimeSeatsDto
{
    public long ShowtimeId { get; init; }
    public int Capacity { get; init; }
    public IReadOnlyList<SeatDto> TakenSeats { get; init; } = [];
}

public sealed record SeatDto(byte Row, byte Seat);
