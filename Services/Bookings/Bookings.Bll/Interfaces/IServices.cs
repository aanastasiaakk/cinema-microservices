using Bookings.Bll.Dto;

namespace Bookings.Bll.Interfaces;

public interface ICustomerService
{
    Task<CustomerDto> GetAsync(long id, CancellationToken ct);
    Task<IReadOnlyList<CustomerDto>> SearchAsync(string? name, int page, int pageSize, CancellationToken ct);
    Task<CustomerDto> CreateAsync(CreateCustomerDto input, CancellationToken ct);
    Task<CustomerDto> UpdateAsync(long id, UpdateCustomerDto input, CancellationToken ct);
    Task DeleteAsync(long id, CancellationToken ct);
}

public interface IBookingService
{
    Task<BookingDto> GetAsync(long id, CancellationToken ct);
    Task<IReadOnlyList<BookingSummaryDto>> GetByCustomerAsync(long customerId, string? status, int page, int pageSize, CancellationToken ct);
    /// <summary>Транзакційно: Bookings + BookingDetails + Tickets (знімок назви й ціни з Showtimes).</summary>
    Task<BookingDto> CreateAsync(CreateBookingDto input, CancellationToken ct);
    /// <summary>Created → AwaitingPayment.</summary>
    Task SubmitAsync(long id, CancellationToken ct);
    /// <summary>Paid → CheckedIn.</summary>
    Task CheckInAsync(long id, CancellationToken ct);
    /// <summary>Created / AwaitingPayment → Cancelled.</summary>
    Task CancelAsync(long id, string? reason, CancellationToken ct);
    Task DeleteAsync(long id, CancellationToken ct);
}

public interface IPaymentService
{
    /// <summary>Транзакційно: Payments (новий запис) + Bookings (статус Paid через процедуру). Ідемпотентно за ключем.</summary>
    Task<PaymentResult> PayAsync(long bookingId, string idempotencyKey, PayBookingDto input, CancellationToken ct);
    Task<IReadOnlyList<PaymentDto>> GetByBookingAsync(long bookingId, CancellationToken ct);
}

public interface IShowtimeService
{
    Task<IReadOnlyList<ShowtimeDto>> GetUpcomingAsync(DateTime? from, int days, CancellationToken ct);
    Task<ShowtimeDto> GetAsync(long id, CancellationToken ct);
    Task<ShowtimeSeatsDto> GetSeatsAsync(long id, CancellationToken ct);
}

/// <summary>Результат оплати: Replayed = true, якщо запит з тим самим ключем уже виконувався.</summary>
public sealed record PaymentResult(PaymentDto Payment, bool Replayed);
