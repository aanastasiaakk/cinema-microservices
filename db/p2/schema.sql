/* =====================================================================
   Проєкт №2 · Контекст «Каталог фільмів і розклад» (Catalog)
   СУБД: SQL Server · доступ у Лаб. 03: EF Core (Code First + Fluent API)
   БД: CinemaCatalog
   Скрипт ідемпотентний. Іменування обмежень і індексів — у стилі EF Core
   (PK_<Table>, FK_<Dependent>_<Principal>_<Column>, IX_<Table>_<Column>),
   щоб у Лаб. 03 модель EF Core (Fluent API) відтворила цю схему першою
   міграцією InitialCatalog, а всі подальші зміни йшли новими міграціями
   (dotnet ef migrations add <Name> -> dotnet ef database update).
   Цей скрипт — спроєктована цільова схема; у Лаб. 03 БД створюватиме EF.

   Сутності (6): Genre, Movie, MovieDetail, MovieGenre, Hall, Showtime
   Зв'язки:
     1:1  Movies  <-> MovieDetails   (FK MovieDetails.MovieId = PK)
     1:N  Movies   -> Showtimes
     1:N  Halls    -> Showtimes
     M:N  Movies  <-> Genres  через MovieGenres (власні поля AddedAt, IsPrimary)

   Каталог — ВЛАСНИК даних про фільми, зали й сеанси. Інші контексти
   зберігають лише Id і копії окремих полів (див. таблицю дублювання).
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF DB_ID(N'CinemaCatalog') IS NULL
    CREATE DATABASE CinemaCatalog;
GO
USE CinemaCatalog;
GO

/* ---------- Genres — довідник жанрів ---------- */
IF OBJECT_ID(N'dbo.Genres', N'U') IS NULL
CREATE TABLE dbo.Genres (
    Id    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Genres PRIMARY KEY,  -- малий довідник: INT достатньо
    Name  NVARCHAR(50)  NOT NULL,
    Slug  VARCHAR(50)   NOT NULL,
    CONSTRAINT CK_Genres_Slug CHECK (Slug NOT LIKE '%[^a-z0-9-]%')
);
GO
-- Запит: пошук жанру за назвою в адмінці; унікальність назви
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Genres_Name')
CREATE UNIQUE INDEX IX_Genres_Name ON dbo.Genres (Name);
-- Запит: сторінка каталогу /genre/{slug}
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Genres_Slug')
CREATE UNIQUE INDEX IX_Genres_Slug ON dbo.Genres (Slug);
GO

/* ---------- Movies — фільми (корінь агрегату Movie = Movie + MovieDetail + MovieGenres) ---------- */
IF OBJECT_ID(N'dbo.Movies', N'U') IS NULL
CREATE TABLE dbo.Movies (
    Id             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Movies PRIMARY KEY,
    Title          NVARCHAR(200) NOT NULL,
    ReleaseYear    SMALLINT      NOT NULL,
    DurationMin    SMALLINT      NOT NULL,
    AgeRating      VARCHAR(5)    NOT NULL CONSTRAINT DF_Movies_AgeRating DEFAULT '0+',
    BasePrice      DECIMAL(10,2) NOT NULL,
    IsActive       BIT           NOT NULL CONSTRAINT DF_Movies_IsActive DEFAULT 1,
    -- копія з контексту Reviews (MongoDB), оновлюється подіями MovieRatingChanged
    AverageRating  DECIMAL(3,2)  NULL,
    ReviewsCount   INT           NOT NULL CONSTRAINT DF_Movies_ReviewsCount DEFAULT 0,
    CreatedAt      DATETIME2(3)  NOT NULL CONSTRAINT DF_Movies_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt      DATETIME2(3)  NULL,
    RowVersion     ROWVERSION    NOT NULL,   -- маркер конкурентності для EF Core ([Timestamp])
    CONSTRAINT CK_Movies_ReleaseYear   CHECK (ReleaseYear BETWEEN 1895 AND 2100),
    CONSTRAINT CK_Movies_DurationMin   CHECK (DurationMin BETWEEN 1 AND 600),
    CONSTRAINT CK_Movies_AgeRating     CHECK (AgeRating IN ('0+', '6+', '12+', '16+', '18+')),
    CONSTRAINT CK_Movies_BasePrice     CHECK (BasePrice > 0),
    CONSTRAINT CK_Movies_AverageRating CHECK (AverageRating IS NULL OR AverageRating BETWEEN 1 AND 5),
    CONSTRAINT CK_Movies_ReviewsCount  CHECK (ReviewsCount >= 0)
);
GO
-- Запит: «чи вже є такий фільм» при імпорті + пошук за назвою (LIKE 'Дюна%')
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Movies_Title_ReleaseYear')
CREATE UNIQUE INDEX IX_Movies_Title_ReleaseYear ON dbo.Movies (Title, ReleaseYear);
-- Запит: афіша — лише активні фільми, відсортовані за рейтингом
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Movies_Active_Rating')
CREATE INDEX IX_Movies_Active_Rating ON dbo.Movies (AverageRating DESC)
    INCLUDE (Title, AgeRating, BasePrice)
    WHERE IsActive = 1;
GO

/* ---------- MovieDetails — 1:1 з Movies (рідко потрібні «важкі» поля) ---------- */
IF OBJECT_ID(N'dbo.MovieDetails', N'U') IS NULL
CREATE TABLE dbo.MovieDetails (
    MovieId      BIGINT         NOT NULL CONSTRAINT PK_MovieDetails PRIMARY KEY
        CONSTRAINT FK_MovieDetails_Movies_MovieId REFERENCES dbo.Movies(Id) ON DELETE CASCADE,
    Description  NVARCHAR(2000) NOT NULL,
    Director     NVARCHAR(200)  NULL,
    Country      NVARCHAR(100)  NULL,
    TrailerUrl   NVARCHAR(500)  NULL,
    PosterUrl    NVARCHAR(500)  NULL,
    CONSTRAINT CK_MovieDetails_TrailerUrl CHECK (TrailerUrl IS NULL OR TrailerUrl LIKE 'https://%')
);
GO

/* ---------- MovieGenres — M:N Movies <-> Genres з власними полями ---------- */
IF OBJECT_ID(N'dbo.MovieGenres', N'U') IS NULL
CREATE TABLE dbo.MovieGenres (
    MovieId    BIGINT       NOT NULL
        CONSTRAINT FK_MovieGenres_Movies_MovieId REFERENCES dbo.Movies(Id) ON DELETE CASCADE,
    GenreId    INT          NOT NULL
        CONSTRAINT FK_MovieGenres_Genres_GenreId REFERENCES dbo.Genres(Id) ON DELETE NO ACTION,
    AddedAt    DATETIME2(3) NOT NULL CONSTRAINT DF_MovieGenres_AddedAt DEFAULT SYSUTCDATETIME(), -- власне поле
    IsPrimary  BIT          NOT NULL CONSTRAINT DF_MovieGenres_IsPrimary DEFAULT 0,               -- власне поле
    CONSTRAINT PK_MovieGenres PRIMARY KEY (MovieId, GenreId)
);
GO
-- Запит: «усі фільми жанру» (зворотний напрямок M:N; PK покриває лише MovieId->GenreId)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MovieGenres_GenreId')
CREATE INDEX IX_MovieGenres_GenreId ON dbo.MovieGenres (GenreId) INCLUDE (AddedAt);
-- Правило: у фільму не більше одного основного жанру
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MovieGenres_MovieId_Primary')
CREATE UNIQUE INDEX IX_MovieGenres_MovieId_Primary ON dbo.MovieGenres (MovieId) WHERE IsPrimary = 1;
GO

/* ---------- Halls — зали кінотеатру ---------- */
IF OBJECT_ID(N'dbo.Halls', N'U') IS NULL
CREATE TABLE dbo.Halls (
    Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Halls PRIMARY KEY,
    Name        NVARCHAR(100) NOT NULL,
    Rows        TINYINT       NOT NULL,
    SeatsPerRow TINYINT       NOT NULL,
    Format      VARCHAR(10)   NOT NULL CONSTRAINT DF_Halls_Format DEFAULT '2D',
    Capacity    AS (CAST(Rows AS INT) * SeatsPerRow) PERSISTED,   -- обчислюване поле
    CONSTRAINT CK_Halls_Rows        CHECK (Rows BETWEEN 1 AND 50),
    CONSTRAINT CK_Halls_SeatsPerRow CHECK (SeatsPerRow BETWEEN 1 AND 60),
    CONSTRAINT CK_Halls_Format      CHECK (Format IN ('2D', '3D', 'IMAX', '4DX'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Halls_Name')
CREATE UNIQUE INDEX IX_Halls_Name ON dbo.Halls (Name);
GO

/* ---------- Showtimes — сеанси (1:N від Movies і від Halls) ---------- */
IF OBJECT_ID(N'dbo.Showtimes', N'U') IS NULL
CREATE TABLE dbo.Showtimes (
    Id        BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Showtimes PRIMARY KEY,
    -- NO ACTION: фільм із запланованими сеансами не можна видалити
    MovieId   BIGINT        NOT NULL
        CONSTRAINT FK_Showtimes_Movies_MovieId REFERENCES dbo.Movies(Id) ON DELETE NO ACTION,
    HallId    INT           NOT NULL
        CONSTRAINT FK_Showtimes_Halls_HallId   REFERENCES dbo.Halls(Id)  ON DELETE NO ACTION,
    StartsAt  DATETIME2(0)  NOT NULL,
    Price     DECIMAL(10,2) NOT NULL,
    Language  CHAR(2)       NOT NULL CONSTRAINT DF_Showtimes_Language DEFAULT 'uk',
    IsCancelled BIT         NOT NULL CONSTRAINT DF_Showtimes_IsCancelled DEFAULT 0,
    CONSTRAINT CK_Showtimes_Price    CHECK (Price > 0),
    CONSTRAINT CK_Showtimes_Language CHECK (Language IN ('uk', 'en'))
);
GO
-- Правило + запит: у залі не може бути двох сеансів в один час
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Showtimes_HallId_StartsAt')
CREATE UNIQUE INDEX IX_Showtimes_HallId_StartsAt ON dbo.Showtimes (HallId, StartsAt);
-- Запит: «розклад фільму» на сторінці фільму (найближчі не скасовані сеанси)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Showtimes_MovieId_StartsAt')
CREATE INDEX IX_Showtimes_MovieId_StartsAt ON dbo.Showtimes (MovieId, StartsAt)
    INCLUDE (HallId, Price)
    WHERE IsCancelled = 0;
GO

/* Представлення для читання: афіша (фільм + основний жанр + кількість сеансів) */
CREATE OR ALTER VIEW dbo.vw_Billboard
AS
SELECT m.Id, m.Title, m.AgeRating, m.AverageRating,
       g.Name AS PrimaryGenre,
       (SELECT COUNT(*) FROM dbo.Showtimes s
        WHERE s.MovieId = m.Id AND s.IsCancelled = 0) AS ShowtimesCount
FROM dbo.Movies m
LEFT JOIN dbo.MovieGenres mg ON mg.MovieId = m.Id AND mg.IsPrimary = 1
LEFT JOIN dbo.Genres g       ON g.Id = mg.GenreId
WHERE m.IsActive = 1;
GO
