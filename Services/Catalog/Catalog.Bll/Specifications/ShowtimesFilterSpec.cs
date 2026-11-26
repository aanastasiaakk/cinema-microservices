using Ardalis.Specification;
using Catalog.Bll.Dto;
using Catalog.Domain.Entities;

namespace Catalog.Bll.Specifications;

/// <summary>
/// ДРУГА специфікація з іншими критеріями (розклад сеансів). Працює через той самий
/// GenericRepository.ListAsync / CountAsync (WithSpecification) без жодних змін у репозиторії.
/// </summary>
public sealed class ShowtimesFilterSpec : Specification<Showtime>
{
    public static readonly IReadOnlyList<string> SortFields = ["startsAt", "price"];

    public ShowtimesFilterSpec(ShowtimeQuery q, int pageSize)
    {
        Query.AsNoTracking();
        Query.Include(s => s.Movie).Include(s => s.Hall);                 // reference-навігації: JOIN

        if (q.MovieId is not null) Query.Where(s => s.MovieId == q.MovieId);
        if (q.HallId is not null) Query.Where(s => s.HallId == q.HallId);
        if (q.From is not null) Query.Where(s => s.StartsAt >= q.From);
        if (q.To is not null) Query.Where(s => s.StartsAt < q.To);
        if (!q.IncludeCancelled) Query.Where(s => !s.IsCancelled);

        var desc = string.Equals(q.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        if (string.Equals(q.SortBy, "price", StringComparison.OrdinalIgnoreCase))
        {
            if (desc) Query.OrderByDescending(s => s.Price).ThenBy(s => s.StartsAt);
            else Query.OrderBy(s => s.Price).ThenBy(s => s.StartsAt);
        }
        else
        {
            if (desc) Query.OrderByDescending(s => s.StartsAt).ThenBy(s => s.Id);
            else Query.OrderBy(s => s.StartsAt).ThenBy(s => s.Id);
        }

        Query.Skip((q.Page - 1) * pageSize).Take(pageSize);
    }
}

/// <summary>Майбутні не скасовані сеанси фільму — використовується правилом «зняти з афіші».</summary>
public sealed class UpcomingShowtimesOfMovieSpec : Specification<Showtime>
{
    public UpcomingShowtimesOfMovieSpec(long movieId, DateTime nowUtc) =>
        Query.Where(s => s.MovieId == movieId && !s.IsCancelled && s.StartsAt > nowUtc);
}

/// <summary>Усі сеанси фільму (для перевірки перед видаленням).</summary>
public sealed class ShowtimesOfMovieSpec : Specification<Showtime>
{
    public ShowtimesOfMovieSpec(long movieId) => Query.Where(s => s.MovieId == movieId);
}

/// <summary>Один сеанс разом із фільмом і залом.</summary>
public sealed class ShowtimeByIdSpec : Specification<Showtime>
{
    public ShowtimeByIdSpec(long id) => Query.Where(s => s.Id == id).Include(s => s.Movie).Include(s => s.Hall).AsNoTracking();
}
