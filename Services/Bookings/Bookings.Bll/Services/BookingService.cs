using System.Data;
using System.Security.Cryptography;
using AutoMapper;
using Bookings.Bll.Dto;
using Bookings.Bll.Interfaces;
using Bookings.Dal.Interfaces;
using Bookings.Domain.Exceptions;
using Bookings.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Bookings.Bll.Services;

/// <summary>
/// Бізнес-логіка бронювань. Єдиний власник правил: контролер лише передає запит сюди.
/// </summary>
public sealed class BookingService(IUnitOfWork uow, IMapper mapper, TimeProvider clock,
                                   ILogger<BookingService> logger) : IBookingService
{
    private const string User = "bookings-api";

    public async Task<BookingDto> GetAsync(long id, CancellationToken ct)
    {
        var booking = await uow.Bookings.GetWithTicketsAsync(id, ct)
                      ?? throw new NotFoundException($"Booking {id} not found");
        return mapper.Map<BookingDto>(booking);
    }

    public async Task<IReadOnlyList<BookingSummaryDto>> GetByCustomerAsync(
        long customerId, string? status, int page, int pageSize, CancellationToken ct)
    {
        if (status is not null && !BookingStatus.All.Contains(status))
            throw new ValidationException(nameof(status), $"Unknown status '{status}'");
        if (await uow.Customers.GetByIdAsync(customerId, ct) is null)
            throw new NotFoundException($"Customer {customerId} not found");

        var bookings = await uow.Bookings.GetByCustomerAsync(customerId, status, page, pageSize, ct);
        return mapper.Map<IReadOnlyList<BookingSummaryDto>>(bookings);
    }

    /// <summary>
    /// ТРАНЗАКЦІЙНА операція через кілька репозиторіїв (Customers, Showtimes, Bookings):
    /// змінює таблиці Bookings, BookingDetails, Tickets — або все, або нічого.
    /// Назва фільму, час і ціна копіюються в квиток як ЗНІМОК (політика дублювання Лаб. 01).
    /// Якщо будь-яке місце зайняте → BusinessConflictException → відкат: бронювання,
    /// деталі й уже додані квитки не зберігаються.
    /// </summary>
    public async Task<BookingDto> CreateAsync(CreateBookingDto input, CancellationToken ct)
    {
        // бізнес-валідація без БД: одне місце двічі в одному запиті
        var duplicate = input.Tickets
            .GroupBy(t => (t.ShowtimeId, t.SeatRow, t.SeatNumber))
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new ValidationException(nameof(input.Tickets),
                $"Seat {duplicate.Key.SeatRow}-{duplicate.Key.SeatNumber} is requested twice");

        long bookingId;
        await uow.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            _ = await uow.Customers.GetByIdAsync(input.CustomerId, ct)
                ?? throw new NotFoundException($"Customer {input.CustomerId} not found");

            bookingId = await uow.Bookings.AddAsync(new Booking
            {
                CustomerId = input.CustomerId,
                BookingCode = NewBookingCode(),
                Status = BookingStatus.Created,
                CreatedBy = User
            }, ct);

            await uow.Bookings.AddDetailsAsync(new BookingDetails
            {
                BookingId = bookingId,
                Channel = input.Channel,
                ContactPhone = input.ContactPhone,
                Comment = input.Comment
            }, ct);

            var now = clock.GetUtcNow().UtcDateTime;
            foreach (var requested in input.Tickets)
            {
                var showtime = await uow.Showtimes.GetByIdAsync(requested.ShowtimeId, ct)
                               ?? throw new NotFoundException($"Showtime {requested.ShowtimeId} not found");
                if (showtime.StartsAt <= now)
                    throw new BusinessConflictException($"Showtime {showtime.Id} has already started");
                if (await uow.Bookings.IsSeatTakenAsync(showtime.Id, requested.SeatRow, requested.SeatNumber, ct))
                    throw new BusinessConflictException(
                        $"Seat {requested.SeatRow}-{requested.SeatNumber} for showtime {showtime.Id} is already taken");

                await uow.Bookings.AddTicketAsync(new Ticket
                {
                    BookingId = bookingId,
                    ShowtimeId = showtime.Id,
                    SeatRow = requested.SeatRow,
                    SeatNumber = requested.SeatNumber,
                    TicketType = requested.TicketType,
                    // знімок на момент бронювання — далі не оновлюється
                    MovieTitle = showtime.MovieTitle,
                    StartsAt = showtime.StartsAt,
                    UnitPrice = Math.Round(showtime.BasePrice * TicketTypes.PriceFactor(requested.TicketType), 2)
                }, ct);
            }

            await uow.Bookings.RecalculateTotalAsync(bookingId, User, ct);
            await uow.CommitAsync(ct);
        }
        catch
        {
            await uow.RollbackAsync(ct);   // жодних «половинчастих» бронювань
            throw;
        }

        logger.LogInformation("Booking {BookingId} created with {Tickets} tickets", bookingId, input.Tickets.Count);
        return await GetAsync(bookingId, ct);
    }

    public Task SubmitAsync(long id, CancellationToken ct) =>
        ChangeStatusAsync(id, BookingStatus.AwaitingPayment, null, ct);

    public Task CheckInAsync(long id, CancellationToken ct) =>
        ChangeStatusAsync(id, BookingStatus.CheckedIn, null, ct);

    public Task CancelAsync(long id, string? reason, CancellationToken ct) =>
        ChangeStatusAsync(id, BookingStatus.Cancelled, reason, ct);

    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        // процедура usp_Booking_SoftDelete: 50001 -> 404, 50020 (не скасоване) -> 409
        await uow.Bookings.SoftDeleteAsync(id, User, ct);
    }

    /// <summary>
    /// Допустимість переходу перевіряє процедура Лаб. 01 (usp_Booking_ChangeStatus):
    /// недопустимий перехід → THROW 50002 → DAL → BusinessConflictException → HTTP 409.
    /// </summary>
    private async Task ChangeStatusAsync(long id, string newStatus, string? reason, CancellationToken ct)
    {
        await uow.Bookings.ChangeStatusAsync(id, newStatus, reason, expectedRowVersion: null, User, ct);
        logger.LogInformation("Booking {BookingId} -> {Status}", id, newStatus);
    }

    private static string NewBookingCode() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(4));   // 8 символів, UNIQUE у БД
}
