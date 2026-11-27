using Catalog.Bll.Diagnostics;
using AutoMapper;
using Catalog.Bll.Dto;
using Catalog.Bll.Interfaces;
using Catalog.Bll.Specifications;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Catalog.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace Catalog.Bll.Services;

/// <summary>
/// Бізнес-операції з фільмами. Працює ЛИШЕ через Unit of Work і репозиторії (без DbContext).
/// Правила самої сутності (ціна &gt; 0, один основний жанр) перевіряє Movie; сервіс перевіряє правила,
/// для яких потрібні інші дані з БД (унікальність назви+року, існування жанрів, наявність сеансів).
/// </summary>
public sealed class MovieService(IUnitOfWork uow, IMapper mapper, TimeProvider clock,
                                 ILogger<MovieService> logger) : IMovieService
{
    public async Task<PagedResult<MovieListItemDto>> GetListAsync(MovieQuery query, CancellationToken ct)
    {
        var pageSize = Paging.ClampPageSize(query.PageSize);
        var spec = new MoviesFilterSpec(query, pageSize);

        var total = await uow.Movies.CountAsync(spec, ct);          // COUNT(*) рахує БД
        var items = await uow.Movies.ListAsync(spec, ct);           // лише потрібна сторінка: OFFSET … FETCH

        return new PagedResult<MovieListItemDto>(mapper.Map<IReadOnlyList<MovieListItemDto>>(items),
                                                 total, query.Page, pageSize);
    }

    public async Task<MovieDto> GetByIdAsync(long id, CancellationToken ct)
        => mapper.Map<MovieDto>(await GetWithDetailsOrThrowAsync(id, ct));

    public async Task<MovieDto> CreateAsync(CreateMovieDto input, CancellationToken ct)
    {
        using var activity = CatalogTelemetry.Source.StartActivity("CreateMovie");
        activity?.SetTag("movie.release_year", input.ReleaseYear);
        activity?.SetTag("movie.genres_count", input.GenreIds.Count);
        if (await uow.Movies.ExistsByTitleAndYearAsync(input.Title.Trim(), input.ReleaseYear, ct: ct))
            throw new ConflictException($"Movie '{input.Title}' ({input.ReleaseYear}) already exists");

        var genres = await uow.Genres.GetAllAsync(ct);
        var missing = input.GenreIds.Except(genres.Select(g => g.Id)).ToList();
        if (missing.Count > 0)
            throw new ValidationException(nameof(input.GenreIds), $"Unknown genre ids: {string.Join(", ", missing)}");

        var now = clock.GetUtcNow().UtcDateTime;
        var movie = new Movie(input.Title, input.ReleaseYear, input.DurationMin, input.AgeRating, input.BasePrice);
        movie.SetDetail(input.Description, input.Director, input.Country, input.TrailerUrl, input.PosterUrl);
        var primary = input.PrimaryGenreId ?? input.GenreIds.FirstOrDefault();
        foreach (var genreId in input.GenreIds)
            movie.AddGenre(genreId, genreId == primary, now);

        await uow.Movies.AddAsync(movie, ct);
        await uow.SaveChangesAsync(ct);                              // Movies + MovieDetails + MovieGenres — одна транзакція

        activity?.SetTag("movie.id", movie.Id);
        logger.LogInformation("Movie {MovieId} '{Title}' created with {GenreCount} genres, base price {BasePrice}",
            movie.Id, movie.Title, input.GenreIds.Count, movie.BasePrice);
        return await GetByIdAsync(movie.Id, ct);
    }

    public async Task ChangePriceAsync(long id, decimal price, CancellationToken ct)
    {
        using var activity = CatalogTelemetry.Source.StartActivity("ChangeMoviePrice");
        activity?.SetTag("movie.id", id);
        var movie = await uow.Movies.GetByIdAsync(id, ct) ?? throw new EntityNotFoundException(nameof(Movie), id);
        var old = movie.BasePrice;
        movie.ChangePrice(price);                                    // правило ціни — у сутності
        await uow.SaveChangesAsync(ct);
        logger.LogInformation("Movie {MovieId} price changed {OldPrice} -> {NewPrice}", id, old, price);
    }

    public async Task DeactivateAsync(long id, CancellationToken ct)
    {
        var movie = await GetWithDetailsOrThrowAsync(id, ct);       // потрібні сеанси для правила
        movie.Deactivate(clock.GetUtcNow().UtcDateTime);
        await uow.SaveChangesAsync(ct);
        logger.LogInformation("Movie {MovieId} deactivated", id);
    }

    public async Task ActivateAsync(long id, CancellationToken ct)
    {
        var movie = await uow.Movies.GetByIdAsync(id, ct) ?? throw new EntityNotFoundException(nameof(Movie), id);
        movie.Activate();
        await uow.SaveChangesAsync(ct);
    }

    public async Task<MovieDto> AddGenreAsync(long id, AddMovieGenreDto input, CancellationToken ct)
    {
        var movie = await GetWithDetailsOrThrowAsync(id, ct);
        _ = await uow.Genres.GetByIdAsync(input.GenreId, ct)
            ?? throw new EntityNotFoundException(nameof(Genre), input.GenreId);

        movie.AddGenre(input.GenreId, input.IsPrimary, clock.GetUtcNow().UtcDateTime);
        await uow.SaveChangesAsync(ct);
        logger.LogInformation("Genre {GenreId} added to movie {MovieId} (primary: {IsPrimary})",
            input.GenreId, id, input.IsPrimary);
        return await GetByIdAsync(id, ct);
    }

    public async Task RemoveGenreAsync(long id, int genreId, CancellationToken ct)
    {
        var movie = await GetWithDetailsOrThrowAsync(id, ct);
        movie.RemoveGenre(genreId);
        await uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        var movie = await uow.Movies.GetByIdAsync(id, ct) ?? throw new EntityNotFoundException(nameof(Movie), id);
        // правило, що потребує БД: фільм із сеансами не видаляється (FK Restrict) — лише знімається з афіші
        var showtimes = await uow.Showtimes.CountAsync(new ShowtimesOfMovieSpec(id), ct);
        if (showtimes > 0)
            throw new ConflictException($"Movie {id} has {showtimes} showtimes; deactivate it instead of deleting");

        uow.Movies.Delete(movie);
        await uow.SaveChangesAsync(ct);
        logger.LogInformation("Movie {MovieId} deleted", id);
    }

    private async Task<Movie> GetWithDetailsOrThrowAsync(long id, CancellationToken ct)
        => await uow.Movies.GetWithDetailsAsync(id, ct) ?? throw new EntityNotFoundException(nameof(Movie), id);
}
