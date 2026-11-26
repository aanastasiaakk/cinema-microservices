using System.Data.Common;
using Catalog.Bll.Dto;
using Catalog.Bll.Specifications;
using Catalog.Dal;
using Catalog.Dal.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace Catalog.Tests;

/// <summary>
/// Інтеграційні тести проти справжнього SQL Server. Запускаються, лише якщо задано
/// CATALOG_TEST_DB (рядок підключення до ОКРЕМОЇ тестової БД — вона створюється міграцією).
/// </summary>
public sealed class DbFactAttribute : FactAttribute
{
    public static string? ConnectionString => Environment.GetEnvironmentVariable("CATALOG_TEST_DB");
    public DbFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString)) Skip = "Set CATALOG_TEST_DB to run integration tests";
    }
}

/// <summary>Рахує SQL-команди, які EF Core реально виконує.</summary>
public sealed class CommandCounter : DbCommandInterceptor
{
    public List<string> Commands { get; } = [];

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
    {
        Commands.Add(command.CommandText);
        return base.ReaderExecutingAsync(command, eventData, result, ct);
    }
}

public class DatabaseTests
{
    private static CatalogDbContext CreateContext(CommandCounter? counter = null)
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>().UseSqlServer(DbFactAttribute.ConnectionString);
        if (counter is not null) options.AddInterceptors(counter);
        return new CatalogDbContext(options.Options);
    }

    private static async Task EnsureSeededAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        await DbInitializer.SeedAsync(db, NullLogger.Instance);
    }

    [DbFact]
    public async Task Migration_creates_schema_with_composite_key_check_and_unique_index()
    {
        await EnsureSeededAsync();
        await using var db = CreateContext();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        var pk = db.Model.FindEntityType(typeof(Catalog.Domain.Entities.MovieGenre))!.FindPrimaryKey()!;
        Assert.Equal(["MovieId", "GenreId"], pk.Properties.Select(p => p.Name));

        // CHECK-обмеження ціни реально працює в БД
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Movies (Title, ReleaseYear, DurationMin, AgeRating, BasePrice, IsActive) VALUES (N'Bad', 2024, 90, '0+', 0, 1)"));
        Assert.Contains("CK_Movies_BasePrice", ex.Message);
    }

    [DbFact]
    public async Task Seeding_is_idempotent()
    {
        await EnsureSeededAsync();
        int movies, showtimes, genres;
        await using (var db = CreateContext())
        {
            movies = await db.Movies.CountAsync(); showtimes = await db.Showtimes.CountAsync(); genres = await db.Genres.CountAsync();
        }

        await EnsureSeededAsync();                                           // повторний запуск

        await using var again = CreateContext();
        Assert.Equal(movies, await again.Movies.CountAsync());
        Assert.Equal(showtimes, await again.Showtimes.CountAsync());
        Assert.Equal(genres, await again.Genres.CountAsync());
        Assert.True(await again.Movies.AnyAsync(m => m.Id == 1 && m.Title == "Довбуш"));
    }

    [DbFact]
    public async Task Eager_loading_runs_one_query_and_explicit_loading_runs_two()
    {
        await EnsureSeededAsync();

        var eager = new CommandCounter();
        await using (var db = CreateContext(eager))
        {
            var movie = await new UnitOfWork(db).Movies.GetWithDetailsAsync(1);
            Assert.NotNull(movie!.Detail);
            Assert.NotEmpty(movie.MovieGenres);
        }

        var explicitCounter = new CommandCounter();
        await using (var db = CreateContext(explicitCounter))
        {
            var genre = await new UnitOfWork(db).Genres.GetWithMoviesAsync(2);
            Assert.NotEmpty(genre!.MovieGenres);
        }

        Assert.Single(eager.Commands);
        Assert.Equal(2, explicitCounter.Commands.Count);
    }

    [DbFact]
    public async Task Paging_happens_in_sql_and_total_count_is_for_all_rows()
    {
        await EnsureSeededAsync();
        var counter = new CommandCounter();
        await using var db = CreateContext(counter);
        var uow = new UnitOfWork(db);

        var spec = new MoviesFilterSpec(new MovieQuery { Page = 2, PageSize = 1 }, 1);
        var total = await uow.Movies.CountAsync(spec);
        var page = await uow.Movies.ListAsync(spec);

        Assert.Equal(await db.Movies.CountAsync(), total);
        Assert.Single(page);
        Assert.Contains(counter.Commands, c => c.Contains("OFFSET") && c.Contains("FETCH"));
        Assert.Contains(counter.Commands, c => c.Contains("COUNT(*)"));
    }

    [DbFact]
    public async Task ManyToMany_query_goes_through_join_entity_and_its_own_field()
    {
        await EnsureSeededAsync();
        var counter = new CommandCounter();
        await using var db = CreateContext(counter);

        var rows = await new UnitOfWork(db).MovieGenres
            .GetRecentlyAddedMoviesAsync("adventure", new DateTime(2025, 1, 1), take: 10);

        Assert.Contains(rows, r => r.Title == "Довбуш");
        Assert.All(rows, r => Assert.True(r.AddedAt >= new DateTime(2025, 1, 1)));
        Assert.Single(counter.Commands);
        Assert.Contains("[MovieGenres]", counter.Commands[0]);
    }
}
