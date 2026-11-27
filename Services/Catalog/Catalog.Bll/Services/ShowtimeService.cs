using Catalog.Bll.Diagnostics;
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

public sealed class ShowtimeService(IUnitOfWork uow, IMapper mapper, ITwoLevelCache cache, TimeProvider clock,
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

    /// <summary>Cache-aside: catalog:showtime:{id} — L1 → L2 → БД.</summary>
    public Task<ShowtimeDto> GetByIdAsync(long id, CancellationToken ct)
        => cache.GetOrCreateAsync(CacheKeys.Showtime(id), CachePolicy.Showtime, async token =>
        {
            var showtime = await uow.Showtimes.FirstOrDefaultAsync(new ShowtimeByIdSpec(id), token)
                           ?? throw new EntityNotFoundException(nameof(Showtime), id);
            return mapper.Map<ShowtimeDto>(showtime);
        }, ct);

    public async Task<ShowtimeDto> CreateAsync(CreateShowtimeDto input, CancellationToken ct)
    {
        using var activity = CatalogTelemetry.Source.StartActivity("ScheduleShowtime");
        activity?.SetTag("movie.id", input.MovieId);
        activity?.SetTag("hall.id", input.HallId);
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
        // картка фільму містить його сеанси — інвалідуємо одразу після запису
        await cache.InvalidateAsync("ShowtimeScheduled", ct, CacheKeys.Movie(input.MovieId));

        logger.LogInformation("Showtime {ShowtimeId} created: movie {MovieId}, hall {HallId}, {StartsAt}",
            showtime.Id, input.MovieId, input.HallId, input.StartsAt);
        return await GetByIdAsync(showtime.Id, ct);
    }

    public async Task CancelAsync(long id, CancellationToken ct)
    {
        var showtime = await uow.Showtimes.GetByIdAsync(id, ct) ?? throw new EntityNotFoundException(nameof(Showtime), id);
        showtime.Cancel(clock.GetUtcNow().UtcDateTime);
        await uow.SaveChangesAsync(ct);
        await cache.InvalidateAsync("ShowtimeCancelled", ct, CacheKeys.Showtime(id), CacheKeys.Movie(showtime.MovieId));
        logger.LogInformation("Showtime {ShowtimeId} cancelled, status {Status}", id, "Cancelled");
    }

    public Task<IReadOnlyList<HallDto>> GetHallsAsync(CancellationToken ct)
        => cache.GetOrCreateAsync(CacheKeys.Halls, CachePolicy.Reference, async token =>
            (IReadOnlyList<HallDto>)mapper.Map<List<HallDto>>((await uow.Halls.GetAllAsync(token)).OrderBy(h => h.Id)), ct);
}
