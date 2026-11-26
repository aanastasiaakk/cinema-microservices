using System.Data;
using AutoMapper;
using Bookings.Bll.Dto;
using Bookings.Bll.Interfaces;
using Bookings.Dal.Interfaces;
using Bookings.Domain.Exceptions;
using Bookings.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Bookings.Bll.Services;

/// <summary>Оплата бронювання — транзакція через два репозиторії (Payments + Bookings).</summary>
public sealed class PaymentService(IUnitOfWork uow, IMapper mapper, TimeProvider clock,
                                   ILogger<PaymentService> logger) : IPaymentService
{
    private const string User = "bookings-api";

    /// <summary>
    /// 1) Idempotency: якщо оплата з таким ключем уже є — повертаємо її, вдруге не списуємо.
    /// 2) В одній транзакції: INSERT у Payments (репозиторій 1) → зміна статусу на Paid через
    ///    процедуру Лаб. 01 (репозиторій 2). Якщо процедура відмовляє (бронювання не в статусі
    ///    AwaitingPayment → THROW 50002), запис оплати відкочується.
    /// </summary>
    public async Task<PaymentResult> PayAsync(long bookingId, string idempotencyKey, PayBookingDto input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100)
            throw new ValidationException("Idempotency-Key", "Idempotency-Key header is required (max 100 chars)");

        var previous = await uow.Payments.GetByProviderTxnIdAsync(idempotencyKey, ct);
        if (previous is not null)
        {
            if (previous.BookingId != bookingId)
                throw new BusinessConflictException("Idempotency-Key was already used for another booking");
            return new PaymentResult(mapper.Map<PaymentDto>(previous), Replayed: true);
        }

        long paymentId;
        await uow.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var booking = await uow.Bookings.GetByIdAsync(bookingId, ct)
                          ?? throw new NotFoundException($"Booking {bookingId} not found");

            if (input.Amount != booking.TotalAmount)
                throw new ValidationException(nameof(input.Amount),
                    $"Amount must be equal to booking total {booking.TotalAmount:0.00}");

            // репозиторій 1: запис оплати
            paymentId = await uow.Payments.AddAsync(new Payment
            {
                BookingId = bookingId,
                Amount = input.Amount,
                Method = input.Method,
                Status = PaymentStatus.Succeeded,
                ProviderTxnId = idempotencyKey,
                PaidAt = clock.GetUtcNow().UtcDateTime,
                CreatedBy = User
            }, ct);

            // репозиторій 2: процедура перевіряє перехід AwaitingPayment -> Paid і суму оплат
            await uow.Bookings.ChangeStatusAsync(bookingId, BookingStatus.Paid, null,
                expectedRowVersion: booking.RowVersion, User, ct);

            await uow.CommitAsync(ct);
        }
        catch
        {
            await uow.RollbackAsync(ct);   // оплата без зміни статусу не зберігається
            throw;
        }

        logger.LogInformation("Booking {BookingId} paid, payment {PaymentId}", bookingId, paymentId);
        var payment = await uow.Payments.GetByIdAsync(paymentId, ct);
        return new PaymentResult(mapper.Map<PaymentDto>(payment), Replayed: false);
    }

    public async Task<IReadOnlyList<PaymentDto>> GetByBookingAsync(long bookingId, CancellationToken ct)
    {
        if (await uow.Bookings.GetByIdAsync(bookingId, ct) is null)
            throw new NotFoundException($"Booking {bookingId} not found");
        return mapper.Map<IReadOnlyList<PaymentDto>>(await uow.Payments.GetByBookingAsync(bookingId, ct));
    }
}
