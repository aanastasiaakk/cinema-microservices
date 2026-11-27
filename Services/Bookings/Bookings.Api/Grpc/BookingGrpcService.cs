using Bookings.Bll.Dto;
using Bookings.Bll.Interfaces;
using Bookings.Domain.Exceptions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using BookingsGrpc = Bookings.Grpc;

namespace Bookings.Api.Grpc;

/// <summary>
/// gRPC-адаптер над BLL бронювань (ті самі сервіси, що й REST-контролери). Віддає лише потрібне
/// Aggregator: бронювання з квитками і ім'я клієнта (без email/телефону).
/// Доменні винятки → RpcException: NotFound / InvalidArgument / FailedPrecondition.
/// </summary>
public sealed class BookingGrpcService(IBookingService bookings, ICustomerService customers)
    : BookingsGrpc.BookingService.BookingServiceBase
{
    public override Task<BookingsGrpc.BookingReply> GetBooking(BookingsGrpc.GetBookingRequest request, ServerCallContext context)
        => Guard(async () =>
        {
            RequirePositive(request.Id, "id");
            var b = await bookings.GetAsync(request.Id, context.CancellationToken);
            var reply = new BookingsGrpc.BookingReply
            {
                Id = b.Id,
                CustomerId = b.CustomerId,
                BookingCode = b.BookingCode,
                Status = b.Status,
                TotalAmount = Money(b.TotalAmount),
                BookedAt = Utc(b.BookedAt),
                Channel = b.Channel ?? ""
            };
            reply.Tickets.AddRange(b.Tickets.Select(ToReply));
            return reply;
        });

    public override Task<BookingsGrpc.CustomerReply> GetCustomer(BookingsGrpc.GetCustomerRequest request, ServerCallContext context)
        => Guard(async () =>
        {
            RequirePositive(request.Id, "id");
            var c = await customers.GetAsync(request.Id, context.CancellationToken);
            return new BookingsGrpc.CustomerReply { Id = c.Id, FullName = c.FullName };
        });

    private static BookingsGrpc.TicketReply ToReply(TicketDto t) => new()
    {
        ShowtimeId = t.ShowtimeId,
        MovieTitle = t.MovieTitle,
        StartsAt = Utc(t.StartsAt),
        SeatRow = t.SeatRow,
        SeatNumber = t.SeatNumber,
        TicketType = t.TicketType,
        UnitPrice = Money(t.UnitPrice)
    };

    private static Timestamp Utc(DateTime value) => Timestamp.FromDateTime(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static BookingsGrpc.DecimalValue Money(decimal value)
    {
        var units = decimal.ToInt64(decimal.Truncate(value));
        return new BookingsGrpc.DecimalValue { Units = units, Nanos = decimal.ToInt32((value - units) * 1_000_000_000m) };
    }

    private static void RequirePositive(long value, string field)
    {
        if (value <= 0) throw new RpcException(new Status(StatusCode.InvalidArgument, $"{field} must be positive"));
    }

    private static async Task<T> Guard<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (NotFoundException ex) { throw new RpcException(new Status(StatusCode.NotFound, ex.Message)); }
        catch (ValidationException ex) { throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message)); }
        catch (BusinessConflictException ex) { throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message)); }
    }
}
