using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

public sealed class HallRepository(CatalogDbContext context) : GenericRepository<Hall>(context), IHallRepository;

public sealed class ShowtimeRepository(CatalogDbContext context)
    : GenericRepository<Showtime>(context), IShowtimeRepository
{
    public Task<bool> IsHallBusyAsync(int hallId, DateTime startsAt, CancellationToken ct = default)
        => Set.AnyAsync(s => s.HallId == hallId && s.StartsAt == startsAt, ct);
}
