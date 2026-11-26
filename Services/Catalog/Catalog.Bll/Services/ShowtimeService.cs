using AutoMapper;
using Catalog.Bll.Caching;
using Catalog.Bll.Dto;
using Catalog.Bll.Interfaces;
using Catalog.Bll.Specifications;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Catalog.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace Catalog.Bll.Services;

public sealed class ShowtimeService(IUnitOfWork uow, IMapper mapper, CatalogCache cache, TimeProvider clock,
                                    ILogger<ShowtimeService> logger) : IShowtimeService
{
    public async Task<PagedResult<ShowtimeDto>> GetListAsync(ShowtimeQuery query, CancellationToken ct)
    {
        var pageSize = Paging.ClampPageSize(query.PageSize);
        var spec = new ShowtimesFilterSpec(query, pageSize);     // інша специфікація — той самий репозиторій
        var total = await uow.Showtimes.CountAsync(spec, ct);
        var items = await uow.Showtimes.ListAsync(spec, ct);
        return new PagedResult<ShowtimeDto>(mapper.Map<IReadOnlyList<ShowtimeDto>>(items), total, query.Page, pageSize);
    }

    public async Task<ShowtimeDto> GetByIdAsync(long id, CancellationToken ct)
    {
        var showtime = await uow.Showtimes.FirstOrDefaultAsync(new ShowtimeByIdSpec(id), ct)
                       ?? throw new EntityNotFoundException(nameof(Showtime), id);
        return mapper.Map<ShowtimeDto>(showtime);
    }

    public async Task<ShowtimeDto> CreateAsync(CreateShowtimeDto input, CancellationToken ct)
    {
        var movie = await uow.Movies.GetByIdAsync(input.MovieId, ct)
                    ?? throw new EntityNotFoundException(nameof(Movie), input.MovieId);
        _ = await uow.Halls.GetByIdAsync(input.HallId, ct)
            ?? throw new EntityNotFoundException(nameof(Hall), input.HallId);
        if (await uow.Showtimes.IsHallBusyAsync(input.HallId, input.StartsAt, ct))
            throw new ConflictException($"Hall {input.HallId} already has a showtime at {input.StartsAt:yyyy-MM-dd HH:mm}");

        var showtime = new Showtime(movie, input.HallId, input.StartsAt, input.Price, input.Language,
                                    clock.GetUtcNow().UtcDateTime);
        await uow.Showtimes.AddAsync(showtime, ct);
        await uow.SaveChangesAsync(ct);

        logger.LogInformation("Showtime {ShowtimeId} created: movie {MovieId}, hall {HallId}, {StartsAt}",
            showtime.Id, input.MovieId, input.HallId, input.StartsAt);
        return await GetByIdAsync(showtime.Id, ct);
    }

    public async Task CancelAsync(long id, CancellationToken ct)
    {
        var showtime = await uow.Showtimes.GetByIdAsync(id, ct) ?? throw new EntityNotFoundException(nameof(Showtime), id);
        showtime.Cancel(clock.GetUtcNow().UtcDateTime);
        await uow.SaveChangesAsync(ct);
        logger.LogInformation("Showtime {ShowtimeId} cancelled", id);
    }

    public Task<IReadOnlyList<HallDto>> GetHallsAsync(CancellationToken ct)
        => cache.GetOrCreateAsync(CacheKeys.Halls, async token =>
            (IReadOnlyList<HallDto>)mapper.Map<List<HallDto>>((await uow.Halls.GetAllAsync(token)).OrderBy(h => h.Id)), ct);
}
