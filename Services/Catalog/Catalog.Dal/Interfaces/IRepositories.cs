using Catalog.Domain.Entities;

namespace Catalog.Dal.Interfaces;

/// <summary>Фільми — EAGER LOADING: картка фільму з усім пов'язаним ОДНИМ запитом (Include/ThenInclude).</summary>
public interface IMovieRepository : IGenericRepository<Movie>
{
    Task<Movie?> GetWithDetailsAsync(long id, CancellationToken ct = default);
    Task<bool> ExistsByTitleAndYearAsync(string title, short releaseYear, long? exceptId = null, CancellationToken ct = default);
}

/// <summary>Жанри — EXPLICIT LOADING: жанр окремим запитом, фільми жанру довантажуються другим запитом.</summary>
public interface IGenreRepository : IGenericRepository<Genre>
{
    Task<Genre?> GetWithMoviesAsync(int id, CancellationToken ct = default);
    Task<bool> ExistsByNameOrSlugAsync(string name, string slug, CancellationToken ct = default);
}

/// <summary>Зв'язки фільм–жанр — LINQ to Entities через проміжну сутність M:N і її власне поле AddedAt.</summary>
public interface IMovieGenreRepository : IGenericRepository<MovieGenre>
{
    /// <summary>Фільми жанру, яким жанр додано не раніше <paramref name="since"/> (найновіші першими).</summary>
    Task<IReadOnlyList<RecentGenreMovie>> GetRecentlyAddedMoviesAsync(string genreSlug, DateTime since, int take, CancellationToken ct = default);
    /// <summary>Скільки фільмів у кожному жанрі (лише основні або всі).</summary>
    Task<IReadOnlyList<(int GenreId, string GenreName, int MoviesCount)>> CountMoviesPerGenreAsync(bool primaryOnly, CancellationToken ct = default);
}

/// <summary>Рядок вибірки M:N: фільм + власні поля зв'язку (AddedAt, IsPrimary).</summary>
public sealed record RecentGenreMovie(long MovieId, string Title, short ReleaseYear, string AgeRating,
                                      decimal BasePrice, DateTime AddedAt, bool IsPrimary);

public interface IHallRepository : IGenericRepository<Hall>;

public interface IShowtimeRepository : IGenericRepository<Showtime>
{
    /// <summary>Чи зайнятий зал у цей момент не скасованим сеансом (унікальний індекс HallId+StartsAt).</summary>
    Task<bool> IsHallBusyAsync(int hallId, DateTime startsAt, CancellationToken ct = default);
}
