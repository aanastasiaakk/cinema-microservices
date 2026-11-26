namespace Bookings.Domain.Models;

/// <summary>Оплата (dbo.Payments) — 1:N від Bookings.</summary>
public class Payment
{
    public long Id { get; set; }
    public long BookingId { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = "Card";
    public string Status { get; set; } = PaymentStatus.Pending;
    /// <summary>Ідентифікатор транзакції провайдера; він же idempotency-ключ (UNIQUE).</summary>
    public string? ProviderTxnId { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

public static class PaymentStatus
{
    public const string Pending = "Pending";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string Refunded = "Refunded";
}
