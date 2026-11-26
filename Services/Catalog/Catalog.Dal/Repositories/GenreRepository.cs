using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

/// <summary>EXPLICIT LOADING: основна сутність — першим запитом, пов'язані — другим, лише коли потрібні.</summary>
public sealed class GenreRepository(CatalogDbContext context) : GenericRepository<Genre>(context), IGenreRepository
{
    public async Task<Genre?> GetWithMoviesAsync(int id, CancellationToken ct = default)
    {
        var genre = await Set.FirstOrDefaultAsync(g => g.Id == id, ct);         // 1-й запит: жанр
        if (genre is null) return null;

        await Context.Entry(genre)
                     .Collection(g => g.MovieGenres)
                     .Query()
                     .Include(mg => mg.Movie)
                     .LoadAsync(ct);                                             // 2-й запит: фільми жанру
        return genre;
    }

    public Task<bool> ExistsByNameOrSlugAsync(string name, string slug, CancellationToken ct = default)
        => Set.AnyAsync(g => g.Name == name || g.Slug == slug, ct);
}
