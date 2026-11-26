using AutoMapper;
using Bookings.Bll.Dto;
using Bookings.Domain.Models;

namespace Bookings.Bll.Mapping;

// Профілі AutoMapper зібрані централізовано в BLL і реєструються в DI одним викликом
// (AddAutoMapper(... typeof(BookingProfile).Assembly)). Конфігурацію перевіряє тест
// MappingConfigurationTests (AssertConfigurationIsValid).

public sealed class CustomerProfile : Profile
{
    public CustomerProfile()
    {
        CreateMap<Customer, CustomerDto>()
            .ForMember(d => d.RowVersion, o => o.MapFrom(s => Convert.ToBase64String(s.RowVersion)));

        CreateMap<CreateCustomerDto, Customer>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore())
            .ForMember(d => d.UpdatedAt, o => o.Ignore())
            .ForMember(d => d.IsDeleted, o => o.Ignore())
            .ForMember(d => d.RowVersion, o => o.Ignore());

        CreateMap<UpdateCustomerDto, Customer>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore())
            .ForMember(d => d.UpdatedAt, o => o.Ignore())
            .ForMember(d => d.IsDeleted, o => o.Ignore())
            .ForMember(d => d.RowVersion, o => o.Ignore());   // декодується в сервісі з перевіркою формату
    }
}

public sealed class BookingProfile : Profile
{
    public BookingProfile()
    {
        CreateMap<Ticket, TicketDto>();

        CreateMap<Booking, BookingDto>()
            .ForMember(d => d.Channel, o => o.MapFrom(s => s.Details != null ? s.Details.Channel : null))
            .ForMember(d => d.Comment, o => o.MapFrom(s => s.Details != null ? s.Details.Comment : null))
            .ForMember(d => d.CancelReason, o => o.MapFrom(s => s.Details != null ? s.Details.CancelReason : null))
            .ForMember(d => d.RowVersion, o => o.MapFrom(s => Convert.ToBase64String(s.RowVersion)));

        CreateMap<Booking, BookingSummaryDto>();
    }
}

public sealed class PaymentProfile : Profile
{
    public PaymentProfile()
    {
        CreateMap<Payment, PaymentDto>();
        CreateMap<Showtime, ShowtimeDto>();
    }
}
