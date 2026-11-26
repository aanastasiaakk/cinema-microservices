using System.Data;
using Bookings.Bll.Dto;
using Bookings.Bll.Services;
using Bookings.Domain.Exceptions;
using Bookings.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Bookings.Tests;

/// <summary>Unit-тести BLL: DAL/UoW замінено моками (Moq).</summary>
public class BookingServiceTests
{
    private readonly TestHelpers.UowMocks _m = new();

    private BookingService CreateBookingService() =>
        new(_m.Uow.Object, TestHelpers.Mapper, TimeProvider.System, NullLogger<BookingService>.Instance);

    private PaymentService CreatePaymentService() =>
        new(_m.Uow.Object, TestHelpers.Mapper, TimeProvider.System, NullLogger<PaymentService>.Instance);

    private static CreateBookingDto Request(params (long showtime, byte row, byte seat)[] seats) => new()
    {
        CustomerId = 1,
        Tickets = seats.Select(s => new CreateTicketDto { ShowtimeId = s.showtime, SeatRow = s.row, SeatNumber = s.seat }).ToList()
    };

    [Fact]
    public async Task Paying_nonexistent_booking_throws_NotFound_and_rolls_back()
    {
        _m.Bookings.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((Booking?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreatePaymentService().PayAsync(999, "key-1", new PayBookingDto { Amount = 100, Method = "Card" }, default));

        _m.Uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Getting_nonexistent_booking_throws_NotFound()
    {
        _m.Bookings.Setup(r => r.GetWithTicketsAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync((Booking?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => CreateBookingService().GetAsync(42, default));
    }

    [Fact]
    public async Task Same_seat_twice_in_request_is_ValidationException_before_any_db_call()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateBookingService().CreateAsync(Request((1, 5, 5), (1, 5, 5)), default));

        _m.Uow.Verify(u => u.BeginTransactionAsync(It.IsAny<IsolationLevel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Taken_seat_throws_Conflict_and_rolls_back_whole_booking()
    {
        _m.Customers.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Customer { Id = 1, Email = "a@b.c", FullName = "Test" });
        _m.Bookings.Setup(r => r.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>())).ReturnsAsync(10);
        _m.Showtimes.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Showtime { Id = 1, MovieTitle = "Довбуш", StartsAt = DateTime.UtcNow.AddDays(5), BasePrice = 200, Capacity = 100 });
        _m.Bookings.Setup(r => r.IsSeatTakenAsync(1, 1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _m.Bookings.Setup(r => r.IsSeatTakenAsync(1, 1, 2, It.IsAny<CancellationToken>())).ReturnsAsync(true);   // друге місце зайняте

        await Assert.ThrowsAsync<BusinessConflictException>(() =>
            CreateBookingService().CreateAsync(Request((1, 1, 1), (1, 1, 2)), default));

        _m.Bookings.Verify(r => r.AddTicketAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()), Times.Once);
        _m.Uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Ticket_gets_snapshot_of_title_and_price_from_showtime()
    {
        Ticket? saved = null;
        _m.Customers.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Customer { Id = 1, Email = "a@b.c", FullName = "Test" });
        _m.Bookings.Setup(r => r.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>())).ReturnsAsync(10);
        _m.Showtimes.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Showtime { Id = 3, MovieTitle = "Мавка", StartsAt = DateTime.UtcNow.AddDays(1), BasePrice = 150, Capacity = 80 });
        _m.Bookings.Setup(r => r.AddTicketAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .Callback<Ticket, CancellationToken>((t, _) => saved = t);
        _m.Bookings.Setup(r => r.GetWithTicketsAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Booking { Id = 10, RowVersion = new byte[8] });

        var request = Request((3, 2, 4));
        request.Tickets[0].TicketType = TicketTypes.Student;
        await CreateBookingService().CreateAsync(request, default);

        Assert.NotNull(saved);
        Assert.Equal("Мавка", saved!.MovieTitle);
        Assert.Equal(120m, saved.UnitPrice);            // 150 * 0.8 — студентський
        _m.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Repeated_payment_with_same_key_is_not_executed_twice()
    {
        _m.Payments.Setup(r => r.GetByProviderTxnIdAsync("key-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Payment { Id = 5, BookingId = 7, Amount = 100, Status = PaymentStatus.Succeeded });

        var result = await CreatePaymentService().PayAsync(7, "key-1", new PayBookingDto { Amount = 100, Method = "Card" }, default);

        Assert.True(result.Replayed);
        _m.Payments.Verify(r => r.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
