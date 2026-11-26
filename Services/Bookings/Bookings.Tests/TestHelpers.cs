using AutoMapper;
using Bookings.Bll.Mapping;
using Bookings.Dal.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Bookings.Tests;

internal static class TestHelpers
{
    public static IMapper Mapper { get; } = new MapperConfiguration(
        cfg => cfg.AddMaps(typeof(BookingProfile).Assembly), NullLoggerFactory.Instance).CreateMapper();

    /// <summary>Мок Unit of Work з окремими моками кожного репозиторію.</summary>
    public sealed class UowMocks
    {
        public Mock<IUnitOfWork> Uow { get; } = new();
        public Mock<ICustomerRepository> Customers { get; } = new();
        public Mock<IBookingRepository> Bookings { get; } = new();
        public Mock<IShowtimeRepository> Showtimes { get; } = new();
        public Mock<IPaymentRepository> Payments { get; } = new();

        public UowMocks()
        {
            Uow.SetupGet(u => u.Customers).Returns(Customers.Object);
            Uow.SetupGet(u => u.Bookings).Returns(Bookings.Object);
            Uow.SetupGet(u => u.Showtimes).Returns(Showtimes.Object);
            Uow.SetupGet(u => u.Payments).Returns(Payments.Object);
        }
    }
}
