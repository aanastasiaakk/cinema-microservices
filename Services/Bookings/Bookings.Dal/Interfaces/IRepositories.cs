using Bookings.Domain.Models;

namespace Bookings.Dal.Interfaces;

/// <summary>
/// Клієнти — реалізація на ЧИСТОМУ ADO.NET. Свідомо НЕ успадковує IGenericRepository:
/// демонструє ручну роботу з SqlCommand, параметрами і SqlDataReader, а також
/// специфічну семантику (м'яке видалення, оновлення з перевіркою rowversion).
/// </summary>
public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Customer?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<Customer>> SearchAsync(string? name, int page, int pageSize, CancellationToken ct = default);
    Task<long> AddAsync(Customer customer, string user, CancellationToken ct = default);
    /// <summary>Оновлює, лише якщо RowVersion збігається. false — запис змінив хтось інший.</summary>
    Task<bool> UpdateAsync(Customer customer, string user, CancellationToken ct = default);
    Task<bool> SoftDeleteAsync(long id, string user, CancellationToken ct = default);
    Task<bool> HasActiveBookingsAsync(long customerId, CancellationToken ct = default);
}

/// <summary>Бронювання (Dapper). Generic CRUD + специфічні запити: multi-mapping, процедури, квитки.</summary>
public interface IBookingRepository : IGenericRepository<Booking>
{
    /// <summary>Бронювання з деталями (1:1) і квитками (M:N) одним запитом — Dapper multi-mapping.</summary>
    Task<Booking?> GetWithTicketsAsync(long bookingId, CancellationToken ct = default);
    Task<IReadOnlyList<Booking>> GetByCustomerAsync(long customerId, string? status, int page, int pageSize, CancellationToken ct = default);
    Task AddDetailsAsync(BookingDetails details, CancellationToken ct = default);
    /// <summary>Чи зайняте місце не скасованим бронюванням. Блокує діапазон (UPDLOCK, HOLDLOCK) до кінця транзакції.</summary>
    Task<bool> IsSeatTakenAsync(long showtimeId, byte row, byte seat, CancellationToken ct = default);
    Task AddTicketAsync(Ticket ticket, CancellationToken ct = default);
    Task<decimal> RecalculateTotalAsync(long bookingId, string user, CancellationToken ct = default);
    /// <summary>Виклик процедури Лаб. 01 dbo.usp_Booking_ChangeStatus (перевірка переходу, THROW 50002).</summary>
    Task ChangeStatusAsync(long bookingId, string newStatus, string? reason, byte[]? expectedRowVersion, string user, CancellationToken ct = default);
    /// <summary>Виклик процедури dbo.usp_Booking_SoftDelete.</summary>
    Task SoftDeleteAsync(long bookingId, string user, CancellationToken ct = default);
}

/// <summary>Локальна копія сеансів з Каталогу (Dapper).</summary>
public interface IShowtimeRepository : IGenericRepository<Showtime>
{
    Task<IReadOnlyList<Showtime>> GetUpcomingAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<IReadOnlyList<(byte Row, byte Seat)>> GetTakenSeatsAsync(long showtimeId, CancellationToken ct = default);
}

/// <summary>Оплати (Dapper).</summary>
public interface IPaymentRepository : IGenericRepository<Payment>
{
    Task<IReadOnlyList<Payment>> GetByBookingAsync(long bookingId, CancellationToken ct = default);
    /// <summary>Пошук за idempotency-ключем (ProviderTxnId, UNIQUE).</summary>
    Task<Payment?> GetByProviderTxnIdAsync(string providerTxnId, CancellationToken ct = default);
}
