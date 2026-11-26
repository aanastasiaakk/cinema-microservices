using AutoMapper;
using Bookings.Bll.Dto;
using Bookings.Bll.Interfaces;
using Bookings.Dal.Interfaces;
using Bookings.Domain.Exceptions;

namespace Bookings.Bll.Services;

/// <summary>Читання локальної копії розкладу (власник даних — Каталог).</summary>
public sealed class ShowtimeService(IUnitOfWork uow, IMapper mapper, TimeProvider clock) : IShowtimeService
{
    public async Task<IReadOnlyList<ShowtimeDto>> GetUpcomingAsync(DateTime? from, int days, CancellationToken ct)
    {
        var start = from ?? clock.GetUtcNow().UtcDateTime;
        var showtimes = await uow.Showtimes.GetUpcomingAsync(start, start.AddDays(days), ct);
        return mapper.Map<IReadOnlyList<ShowtimeDto>>(showtimes);
    }

    public async Task<ShowtimeDto> GetAsync(long id, CancellationToken ct)
    {
        var showtime = await uow.Showtimes.GetByIdAsync(id, ct)
                       ?? throw new NotFoundException($"Showtime {id} not found");
        return mapper.Map<ShowtimeDto>(showtime);
    }

    public async Task<ShowtimeSeatsDto> GetSeatsAsync(long id, CancellationToken ct)
    {
        var showtime = await uow.Showtimes.GetByIdAsync(id, ct)
                       ?? throw new NotFoundException($"Showtime {id} not found");
        var taken = await uow.Showtimes.GetTakenSeatsAsync(id, ct);
        return new ShowtimeSeatsDto
        {
            ShowtimeId = id,
            Capacity = showtime.Capacity,
            TakenSeats = taken.Select(s => new SeatDto(s.Row, s.Seat)).ToList()
        };
    }
}
