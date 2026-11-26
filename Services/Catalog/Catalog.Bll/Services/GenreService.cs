using AutoMapper;
using Catalog.Bll.Caching;
using Catalog.Bll.Dto;
using Catalog.Bll.Interfaces;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Catalog.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace Catalog.Bll.Services;

public sealed class GenreService(IUnitOfWork uow, IMapper mapper, CatalogCache cache, TimeProvider clock,
                                 ILogger<GenreService> logger) : IGenreService
{
    /// <summary>Довідник жанрів — з кешу (рідко змінюється).</summary>
    public Task<IReadOnlyList<GenreDto>> GetAllAsync(CancellationToken ct)
        => cache.GetOrCreateAsync(CacheKeys.Genres, async token =>
        {
            var genres = await uow.Genres.GetAllAsync(token);
            return (IReadOnlyList<GenreDto>)mapper.Map<List<GenreDto>>(genres.OrderBy(g => g.Name));
        }, ct);

    /// <summary>Explicit Loading: жанр і його фільми — двома окремими запитами.</summary>
    public async Task<GenreWithMoviesDto> GetWithMoviesAsync(int id, CancellationToken ct)
        => mapper.Map<GenreWithMoviesDto>(await uow.Genres.GetWithMoviesAsync(id, ct)
                                          ?? throw new EntityNotFoundException(nameof(Genre), id));

    /// <summary>LINQ через проміжну сутність M:N: фільми, яким жанр додано за останні N днів.</summary>
    public async Task<IReadOnlyList<RecentGenreMovieDto>> GetRecentlyAddedMoviesAsync(string slug, int days, CancellationToken ct)
    {
        if (days is < 1 or > 3650) throw new ValidationException(nameof(days), "days must be between 1 and 3650");
        var genres = await GetAllAsync(ct);
        if (genres.All(g => g.Slug != slug)) throw new EntityNotFoundException(nameof(Genre), slug);

        var since = clock.GetUtcNow().UtcDateTime.AddDays(-days);
        var movies = await uow.MovieGenres.GetRecentlyAddedMoviesAsync(slug, since, take: 50, ct);
        return mapper.Map<IReadOnlyList<RecentGenreMovieDto>>(movies);
    }

    public async Task<IReadOnlyList<GenreStatsDto>> GetStatsAsync(bool primaryOnly, CancellationToken ct)
    {
        var rows = await uow.MovieGenres.CountMoviesPerGenreAsync(primaryOnly, ct);
        return rows.Select(r => new GenreStatsDto(r.GenreId, r.GenreName, r.MoviesCount)).ToList();
    }

    public async Task<GenreDto> CreateAsync(CreateGenreDto input, CancellationToken ct)
    {
        var name = input.Name.Trim();
        if (await uow.Genres.ExistsByNameOrSlugAsync(name, input.Slug, ct))
            throw new ConflictException($"Genre with name '{name}' or slug '{input.Slug}' already exists");

        var genre = new Genre(name, input.Slug);
        await uow.Genres.AddAsync(genre, ct);
        await uow.SaveChangesAsync(ct);
        cache.Invalidate(CacheKeys.Genres);

        logger.LogInformation("Genre {GenreId} '{GenreName}' created", genre.Id, genre.Name);
        return mapper.Map<GenreDto>(genre);
    }
}
