using Catalog.Bll.Dto;

namespace Catalog.Bll.Interfaces;

public interface IMovieService
{
    Task<PagedResult<MovieListItemDto>> GetListAsync(MovieQuery query, CancellationToken ct);
    Task<MovieDto> GetByIdAsync(long id, CancellationToken ct);
    Task<MovieDto> CreateAsync(CreateMovieDto input, CancellationToken ct);
    Task ChangePriceAsync(long id, decimal price, CancellationToken ct);
    Task DeactivateAsync(long id, CancellationToken ct);
    Task ActivateAsync(long id, CancellationToken ct);
    Task<MovieDto> AddGenreAsync(long id, AddMovieGenreDto input, CancellationToken ct);
    Task RemoveGenreAsync(long id, int genreId, CancellationToken ct);
    Task DeleteAsync(long id, CancellationToken ct);
}

public interface IGenreService
{
    Task<IReadOnlyList<GenreDto>> GetAllAsync(CancellationToken ct);
    Task<GenreWithMoviesDto> GetWithMoviesAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<RecentGenreMovieDto>> GetRecentlyAddedMoviesAsync(string slug, int days, CancellationToken ct);
    Task<IReadOnlyList<GenreStatsDto>> GetStatsAsync(bool primaryOnly, CancellationToken ct);
    Task<GenreDto> CreateAsync(CreateGenreDto input, CancellationToken ct);
}

public interface IShowtimeService
{
    Task<PagedResult<ShowtimeDto>> GetListAsync(ShowtimeQuery query, CancellationToken ct);
    Task<ShowtimeDto> GetByIdAsync(long id, CancellationToken ct);
    Task<ShowtimeDto> CreateAsync(CreateShowtimeDto input, CancellationToken ct);
    Task CancelAsync(long id, CancellationToken ct);
    Task<IReadOnlyList<HallDto>> GetHallsAsync(CancellationToken ct);
}
