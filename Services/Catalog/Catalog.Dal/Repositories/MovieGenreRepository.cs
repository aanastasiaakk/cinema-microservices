using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

/// <summary>LINQ to Entities: вибірки M:N через проміжну сутність MovieGenre та її власні поля.</summary>
public sealed class MovieGenreRepository(CatalogDbContext context)
    : GenericRepository<MovieGenre>(context), IMovieGenreRepository
{
    public async Task<IReadOnlyList<RecentGenreMovie>> GetRecentlyAddedMoviesAsync(
        string genreSlug, DateTime since, int take, CancellationToken ct = default)
        => await Set
            .Where(mg => mg.Genre.Slug == genreSlug && mg.AddedAt >= since && mg.Movie.IsActive)
            .OrderByDescending(mg => mg.AddedAt)           // власне поле проміжної сутності
            .Take(take)
            .Select(mg => new RecentGenreMovie(mg.MovieId, mg.Movie.Title, mg.Movie.ReleaseYear,
                                               mg.Movie.AgeRating, mg.Movie.BasePrice, mg.AddedAt, mg.IsPrimary))
            .ToListAsync(ct);                              // один SELECT: MovieGenres JOIN Genres JOIN Movies

    public async Task<IReadOnlyList<(int GenreId, string GenreName, int MoviesCount)>> CountMoviesPerGenreAsync(
        bool primaryOnly, CancellationToken ct = default)
    {
        var rows = await Set
            .Where(mg => !primaryOnly || mg.IsPrimary)
            .GroupBy(mg => new { mg.GenreId, mg.Genre.Name })
            .Select(g => new { g.Key.GenreId, g.Key.Name, Count = g.Count() })
            .OrderByDescending(x => x.Count).ThenBy(x => x.Name)
            .ToListAsync(ct);
        return rows.Select(r => (r.GenreId, r.Name, r.Count)).ToList();
    }
}
