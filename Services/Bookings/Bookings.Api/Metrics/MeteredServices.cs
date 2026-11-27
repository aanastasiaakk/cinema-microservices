using System.Diagnostics;
using Bookings.Bll.Dto;
using Bookings.Bll.Interfaces;
using Bookings.Domain.Models;

namespace Bookings.Api.Metrics;

/// <summary>
/// Декоратори сервісів BLL: вимірюють тривалість КОЖНОЇ операції (успішної і невдалої) і рахують бізнес-події
/// одразу після успіху. BLL не змінюється, а метрики працюють і для REST, і для gRPC.
/// </summary>
public sealed class MeteredBookingService(IBookingService inner, BookingMetrics metrics) : IBookingService
{
    public Task<BookingDto> GetAsync(long id, CancellationToken ct) => Measure("get", () => inner.GetAsync(id, ct));

    public Task<IReadOnlyList<BookingSummaryDto>> GetByCustomerAsync(long customerId, string? status, int page, int pageSize, CancellationToken ct)
        => Measure("list", () => inner.GetByCustomerAsync(customerId, status, page, pageSize, ct));

    public async Task<BookingDto> CreateAsync(CreateBookingDto input, CancellationToken ct)
    {
        var booking = await Measure("create", () => inner.CreateAsync(input, ct));
        metrics.BookingCreated(input.Channel);
        return booking;
    }

    public Task SubmitAsync(long id, CancellationToken ct) => ChangeStatus("submit", BookingStatus.AwaitingPayment, () => inner.SubmitAsync(id, ct));
    public Task CheckInAsync(long id, CancellationToken ct) => ChangeStatus("check_in", BookingStatus.CheckedIn, () => inner.CheckInAsync(id, ct));
    public Task CancelAsync(long id, string? reason, CancellationToken ct) => ChangeStatus("cancel", BookingStatus.Cancelled, () => inner.CancelAsync(id, reason, ct));
    public Task DeleteAsync(long id, CancellationToken ct) => Measure("delete", async () => { await inner.DeleteAsync(id, ct); return true; });

    private async Task ChangeStatus(string operation, string status, Func<Task> call)
    {
        await Measure(operation, async () => { await call(); return true; });
        metrics.StatusChanged(status);
    }

    private async Task<T> Measure<T>(string operation, Func<Task<T>> call)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await call();
            metrics.RecordDuration(operation, success: true, sw.Elapsed.TotalMilliseconds);
            return result;
        }
        catch
        {
            metrics.RecordDuration(operation, success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
    }
}

public sealed class MeteredPaymentService(IPaymentService inner, BookingMetrics metrics) : IPaymentService
{
    public async Task<PaymentResult> PayAsync(long bookingId, string idempotencyKey, PayBookingDto input, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await inner.PayAsync(bookingId, idempotencyKey, input, ct);
            metrics.RecordDuration("pay", success: true, sw.Elapsed.TotalMilliseconds);
            if (!result.Replayed)                                         // повтор з тим самим ключем — не нова оплата
            {
                metrics.BookingPaid(input.Method, input.Amount);
                metrics.StatusChanged(BookingStatus.Paid);
            }
            return result;
        }
        catch
        {
            metrics.RecordDuration("pay", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
    }

    public Task<IReadOnlyList<PaymentDto>> GetByBookingAsync(long bookingId, CancellationToken ct) => inner.GetByBookingAsync(bookingId, ct);
}
