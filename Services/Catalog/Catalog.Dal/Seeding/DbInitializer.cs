using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Catalog.Dal.Seeding;

/// <summary>
/// Програмний сідінг ПІСЛЯ MigrateAsync (довідники Genres і Halls уже вставила міграція через HasData).
/// ІДЕМПОТЕНТНИЙ: кожен запис перевіряється за природним ключем перед вставкою
/// (Movies: Title+ReleaseYear; Showtimes: HallId+StartsAt), тому повторний запуск нічого не дублює.
/// Порядок дотримується FK: спочатку фільми (з деталями й жанрами), потім сеанси.
/// Фільми й сеанси вставляються по одному, щоб на порожній БД отримати Id 1–3 і 1–4 —
/// саме на ці Id посилаються CinemaBooking.Showtimes і відгуки в MongoDB (Лаб. 01).
/// </summary>
public static class DbInitializer
{
    /// <summary>«Поточний момент» для доменних правил сідінгу: дані описують сеанси 2027 року.</summary>
    private static readonly DateTime SeedClock = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private sealed record MovieSeed(string Title, short Year, short Duration, string Age, decimal Price,
                                    string Description, string Director, string Country,
                                    (int GenreId, bool Primary)[] Genres);

    private static readonly MovieSeed[] Movies =
    [
        new("Довбуш", 2023, 120, "12+", 180.00m,
            "Історична драма про ватажка опришків у Карпатах XVIII століття.", "Олесь Санін", "Україна",
            [(5, true), (1, false), (2, false)]),
        new("Мавка. Лісова пісня", 2023, 99, "0+", 150.00m,
            "Анімаційна історія за мотивами драми-феєрії Лесі Українки.", "Олександра Рубан", "Україна",
            [(3, true), (6, false)]),
        new("Дюна: Частина друга", 2024, 166, "12+", 220.00m,
            "Продовження науково-фантастичної саги про планету Арракіс.", "Дені Вільнев", "США",
            [(4, true), (2, false)]),
    ];

    private static readonly (string MovieTitle, int HallId, DateTime StartsAt, decimal Price)[] Showtimes =
    [
        ("Довбуш", 1, new DateTime(2027, 1, 15, 18, 0, 0), 220.00m),
        ("Довбуш", 2, new DateTime(2027, 1, 15, 21, 0, 0), 180.00m),
        ("Мавка. Лісова пісня", 2, new DateTime(2027, 1, 16, 12, 0, 0), 150.00m),
        ("Дюна: Частина друга", 1, new DateTime(2027, 1, 16, 19, 30, 0), 250.00m),
    ];

    public static async Task SeedAsync(CatalogDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var added = 0;

        foreach (var seed in Movies)
        {
            if (await db.Movies.AnyAsync(m => m.Title == seed.Title && m.ReleaseYear == seed.Year, ct))
                continue;                                                    // уже є — не дублюємо

            var movie = new Movie(seed.Title, seed.Year, seed.Duration, seed.Age, seed.Price);
            movie.SetDetail(seed.Description, seed.Director, seed.Country, trailerUrl: null, posterUrl: null);
            foreach (var (genreId, primary) in seed.Genres)
                movie.AddGenre(genreId, primary, SeedClock);
            db.Movies.Add(movie);
            await db.SaveChangesAsync(ct);                                   // по одному: детерміновані Id
            added++;
        }

        foreach (var (title, hallId, startsAt, price) in Showtimes)
        {
            if (await db.Showtimes.AnyAsync(s => s.HallId == hallId && s.StartsAt == startsAt, ct))
                continue;

            var movie = await db.Movies.SingleAsync(m => m.Title == title, ct);
            db.Showtimes.Add(new Showtime(movie, hallId, startsAt, price, "uk", SeedClock));
            await db.SaveChangesAsync(ct);
            added++;
        }

        if (added > 0) logger.LogInformation("Catalog seed: {AddedCount} records added", added);
        else logger.LogInformation("Catalog seed: database already contains seed data, nothing to add");
    }
}
