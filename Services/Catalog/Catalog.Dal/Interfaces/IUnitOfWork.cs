namespace Catalog.Dal.Interfaces;

/// <summary>
/// Unit of Work: набір репозиторіїв над ОДНИМ DbContext і ЄДИНИЙ SaveChangesAsync, що
/// зберігає всі зміни бізнес-операції однією транзакцією БД.
/// (В eShop цю роль виконує сам OrderingContext, що реалізує IUnitOfWork з SeedWork.)
/// </summary>
public interface IUnitOfWork
{
    IMovieRepository Movies { get; }
    IGenreRepository Genres { get; }
    IMovieGenreRepository MovieGenres { get; }
    IHallRepository Halls { get; }
    IShowtimeRepository Showtimes { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
