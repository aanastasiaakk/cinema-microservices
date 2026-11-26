/* =====================================================================
   Проєкт №2 · CinemaCatalog · Довідникові (початкові) дані
   ІДЕМПОТЕНТНО: MERGE / IF NOT EXISTS за природними ключами
   (Genres.Slug, Movies.Title+ReleaseYear, Halls.Name, MovieGenres PK).
   Фільми, зали й сеанси вставляються з ФІКСОВАНИМИ Id (IDENTITY_INSERT),
   бо на ці Id посилаються інші бази (CinemaBooking.Showtimes, MongoDB reviews).
   У Лаб. 03 ці ж дані задаються через HasData(...) у Fluent API.
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
USE CinemaCatalog;
GO
SET NOCOUNT ON;

/* ---------- Жанри ---------- */
SET IDENTITY_INSERT dbo.Genres ON;
MERGE dbo.Genres AS t
USING (VALUES
    (1, N'Драма',      'drama'),
    (2, N'Пригоди',    'adventure'),
    (3, N'Анімація',   'animation'),
    (4, N'Фантастика', 'sci-fi'),
    (5, N'Історичний', 'historical'),
    (6, N'Сімейний',   'family')
) AS s (Id, Name, Slug)
ON t.Id = s.Id
WHEN NOT MATCHED THEN INSERT (Id, Name, Slug) VALUES (s.Id, s.Name, s.Slug);
SET IDENTITY_INSERT dbo.Genres OFF;
GO

/* ---------- Фільми ---------- */
SET IDENTITY_INSERT dbo.Movies ON;
MERGE dbo.Movies AS t
USING (VALUES
    (1, N'Довбуш',              2023, 120, '12+', 180.00),
    (2, N'Мавка. Лісова пісня', 2023,  99, '0+',  150.00),
    (3, N'Дюна: Частина друга', 2024, 166, '12+', 220.00)
) AS s (Id, Title, ReleaseYear, DurationMin, AgeRating, BasePrice)
ON t.Id = s.Id
WHEN NOT MATCHED THEN
    INSERT (Id, Title, ReleaseYear, DurationMin, AgeRating, BasePrice)
    VALUES (s.Id, s.Title, s.ReleaseYear, s.DurationMin, s.AgeRating, s.BasePrice);
SET IDENTITY_INSERT dbo.Movies OFF;
GO

/* ---------- Деталі фільмів (1:1) ---------- */
MERGE dbo.MovieDetails AS t
USING (VALUES
    (1, N'Історична драма про ватажка опришків у Карпатах XVIII століття.', N'Олесь Санін', N'Україна'),
    (2, N'Анімаційна історія за мотивами драми-феєрії Лесі Українки.',     N'Олександра Рубан', N'Україна'),
    (3, N'Продовження науково-фантастичної саги про планету Арракіс.',     N'Дені Вільнев', N'США')
) AS s (MovieId, Description, Director, Country)
ON t.MovieId = s.MovieId
WHEN NOT MATCHED THEN
    INSERT (MovieId, Description, Director, Country)
    VALUES (s.MovieId, s.Description, s.Director, s.Country);
GO

/* ---------- Жанри фільмів (M:N з власними полями) ---------- */
MERGE dbo.MovieGenres AS t
USING (VALUES
    (1, 5, 1), (1, 1, 0), (1, 2, 0),
    (2, 3, 1), (2, 6, 0),
    (3, 4, 1), (3, 2, 0)
) AS s (MovieId, GenreId, IsPrimary)
ON t.MovieId = s.MovieId AND t.GenreId = s.GenreId
WHEN NOT MATCHED THEN
    INSERT (MovieId, GenreId, IsPrimary) VALUES (s.MovieId, s.GenreId, s.IsPrimary);
GO

/* ---------- Зали ---------- */
SET IDENTITY_INSERT dbo.Halls ON;
MERGE dbo.Halls AS t
USING (VALUES
    (1, N'Зал 1 (IMAX)', 10, 12, 'IMAX'),
    (2, N'Зал 2',         8, 10, '2D')
) AS s (Id, Name, Rows, SeatsPerRow, Format)
ON t.Id = s.Id
WHEN NOT MATCHED THEN
    INSERT (Id, Name, Rows, SeatsPerRow, Format)
    VALUES (s.Id, s.Name, s.Rows, s.SeatsPerRow, s.Format);
SET IDENTITY_INSERT dbo.Halls OFF;
GO

/* ---------- Сеанси (Id збігаються з CinemaBooking.Showtimes) ---------- */
SET IDENTITY_INSERT dbo.Showtimes ON;
MERGE dbo.Showtimes AS t
USING (VALUES
    (1, 1, 1, CAST('2027-01-15T18:00:00' AS DATETIME2(0)), 220.00),
    (2, 1, 2, CAST('2027-01-15T21:00:00' AS DATETIME2(0)), 180.00),
    (3, 2, 2, CAST('2027-01-16T12:00:00' AS DATETIME2(0)), 150.00),
    (4, 3, 1, CAST('2027-01-16T19:30:00' AS DATETIME2(0)), 250.00)
) AS s (Id, MovieId, HallId, StartsAt, Price)
ON t.Id = s.Id
WHEN NOT MATCHED THEN
    INSERT (Id, MovieId, HallId, StartsAt, Price)
    VALUES (s.Id, s.MovieId, s.HallId, s.StartsAt, s.Price);
SET IDENTITY_INSERT dbo.Showtimes OFF;
GO

PRINT N'Seed CinemaCatalog: OK';
GO
