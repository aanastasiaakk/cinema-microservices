using Catalog.Dal.Interfaces;
using Catalog.Dal.Repositories;

namespace Catalog.Dal;

/// <summary>
/// Unit of Work над одним CatalogDbContext (Scoped = один на HTTP-запит).
/// Репозиторії лише додають/змінюють/видаляють сутності в контексті; SaveChangesAsync викликається
/// ОДИН раз наприкінці бізнес-операції, і EF Core записує всі зміни в одній транзакції БД.
/// </summary>
public sealed class UnitOfWork(CatalogDbContext context) : IUnitOfWork
{
    private IMovieRepository? _movies;
    private IGenreRepository? _genres;
    private IMovieGenreRepository? _movieGenres;
    private IHallRepository? _halls;
    private IShowtimeRepository? _showtimes;

    public IMovieRepository Movies => _movies ??= new MovieRepository(context);
    public IGenreRepository Genres => _genres ??= new GenreRepository(context);
    public IMovieGenreRepository MovieGenres => _movieGenres ??= new MovieGenreRepository(context);
    public IHallRepository Halls => _halls ??= new HallRepository(context);
    public IShowtimeRepository Showtimes => _showtimes ??= new ShowtimeRepository(context);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => context.SaveChangesAsync(ct);
}
