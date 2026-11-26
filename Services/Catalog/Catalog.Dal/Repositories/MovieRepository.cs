using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

/// <summary>EAGER LOADING: пов'язані дані завантажуються ОДНИМ SQL-запитом разом з фільмом.</summary>
public sealed class MovieRepository(CatalogDbContext context) : GenericRepository<Movie>(context), IMovieRepository
{
    /// <summary>
    /// Картка фільму: деталі (1:1), жанри через проміжну сутність (M:N) і сеанси із залами (1:N).
    /// AsSplitQuery НЕ використовується свідомо — у лозі видно рівно один SELECT з JOIN-ами.
    /// </summary>
    public Task<Movie?> GetWithDetailsAsync(long id, CancellationToken ct = default)
        => Set.Include(m => m.Detail)
              .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
              .Include(m => m.Showtimes).ThenInclude(s => s.Hall)
              .FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<bool> ExistsByTitleAndYearAsync(string title, short releaseYear, long? exceptId = null,
                                                CancellationToken ct = default)
        => Set.AnyAsync(m => m.Title == title && m.ReleaseYear == releaseYear &&
                             (exceptId == null || m.Id != exceptId), ct);
}
