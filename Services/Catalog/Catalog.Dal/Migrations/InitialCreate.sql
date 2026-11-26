IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261126150325_InitialCreate'
)
BEGIN
    CREATE TABLE [Genres] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(50) NOT NULL,
        [Slug] varchar(50) NOT NULL,
        CONSTRAINT [PK_Genres] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Genres_Slug] CHECK ([Slug] NOT LIKE '%[^a-z0-9-]%')
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261126150325_InitialCreate'
)
BEGIN
    CREATE TABLE [Halls] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Rows] tinyint NOT NULL,
        [SeatsPerRow] tinyint NOT NULL,
        [Format] varchar(10) NOT NULL DEFAULT '2D',
        [Capacity] AS CAST([Rows] AS INT) * [SeatsPerRow] PERSISTED,
        CONSTRAINT [PK_Halls] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Halls_Format] CHECK ([Format] IN ('2D', '3D', 'IMAX', '4DX')),
        CONSTRAINT [CK_Halls_Rows] CHECK ([Rows] BETWEEN 1 AND 50),
        CONSTRAINT [CK_Halls_SeatsPerRow] CHECK ([SeatsPerRow] BETWEEN 1 AND 60)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261126150325_InitialCreate'
)
BEGIN
    CREATE TABLE [Movies] (
        [Id] bigint NOT NULL IDENTITY,
        [Title] nvarchar(200) NOT NULL,
        [ReleaseYear] smallint NOT NULL,
        [DurationMin] smallint NOT NULL,
        [AgeRating] varchar(5) NOT NULL DEFAULT '0+',
        [BasePrice] decimal(18,2) NOT NULL,
        [IsActive] bit NOT NULL,
        [AverageRating] decimal(3,2) NULL,
        [ReviewsCount] int NOT NULL DEFAULT 0,
        [CreatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2(3) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Movies] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Movies_AgeRating] CHECK ([AgeRating] IN ('0+', '6+', '12+', '16+', '18+')),
        CONSTRAINT [CK_Movies_AverageRating] CHECK ([AverageRating] IS NULL OR [AverageRating] BETWEEN 1 AND 5),
        CONSTRAINT [CK_Movies_BasePrice] CHECK ([BasePrice] > 0),
        CONSTRAINT [CK_Movies_DurationMin] CHECK ([DurationMin] BETWEEN 1 AND 600),
        CONSTRAINT [CK_Movies_ReleaseYear] CHECK ([ReleaseYear] BETWEEN 1895 AND 2100),
        CONSTRAINT [CK_Movies_ReviewsCount] CHECK ([ReviewsCount] >= 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261126150325_InitialCreate'
)
BEGIN
    CREATE TABLE [MovieDetails] (
        [MovieId] bigint NOT NULL,
        [Description] nvarchar(2000) NOT NULL,
        [Director] nvarchar(200) NULL,
        [Country] nvarchar(100) NULL,
        [TrailerUrl] nvarchar(500) NULL,
        [PosterUrl] nvarchar(500) NULL,
        CONSTRAINT [PK_MovieDetails] PRIMARY KEY ([MovieId]),
        CONSTRAINT [CK_MovieDetails_TrailerUrl] CHECK ([TrailerUrl] IS NULL OR [TrailerUrl] LIKE 'https://%'),
        CONSTRAINT [FK_MovieDetails_Movies_MovieId] FOREIGN KEY ([MovieId]) REFERENCES [Movies] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261126150325_InitialCreate'
)
BEGIN
    CREATE TABLE [MovieGenres] (
        [MovieId] bigint NOT NULL,
        [GenreId] int NOT NULL,
        [AddedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [IsPrimary] bit NOT NULL,
        CONSTRAINT [PK_MovieGenres] PRIMARY KEY ([MovieId], [GenreId]),
        CONSTRAINT [FK_MovieGenres_Genres_GenreId] FOREIGN KEY ([GenreId]) REFERENCES [Genres] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MovieGenres_Movies_MovieId] FOREIGN KEY ([MovieId]) REFERENCES [Movies] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261126150325_InitialCreate'
)
BEGIN
    CREATE TABLE [Showtimes] (
        [Id] bigint NOT NULL IDENTITY,
        [MovieId] bigint NOT NULL,
        [HallId] int NOT NULL,
        [StartsAt] datetime2(0) NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [Language] char(2) NOT NULL DEFAULT 'uk',
        [IsCancelled] bit NOT NULL,
        CONSTRAINT [PK_Showtimes] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Showtimes_Language] CHECK ([Language] IN ('uk', 'en')),
        CONSTRAINT [CK_Showtimes_Price] CHECK ([Price] > 0),
        CONSTRAINT [FK_Showtimes_Halls_HallId] FOREIGN KEY ([HallId]) REFERENCES [Halls] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Showtimes_Movies_MovieId] FOREIGN KEY ([MovieId]) REFERENCES [Movies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261126150325_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'Slug') AND [object_id] = OBJECT_ID(N'[Genres]'))
        SET IDENTITY_INSERT [Genres] ON;
    EXEC(N'INSERT INTO [Genres] ([Id], [Name], [Slug])
    VALUES (1, N''Драма'', ''drama''),
    (2, N''Пригоди'', ''adventure''),
    (3, N''Анімація'', ''animation''),
    (4, N''Фантастика'', ''sci-fi''),
    (5, N''Історичний'', ''historical''),
    (6, N''Сімейний'', ''family'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'Slug') AND [object_id] = OBJECT_ID(N'[Genres]'))
        SET IDENTITY_INSERT [Genres] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261126150325_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Format', N'Name', N'Rows', N'SeatsPerRow') AND [object_id] = OBJECT_ID(N'[Halls]'))
        SET IDENTITY_INSERT [Halls] ON;
    EXEC(N'INSERT INTO [Halls] ([Id], [Format], [Name], [Rows], [SeatsPerRow])
    VALUES (1, ''IMAX'', N''Зал 1 (IMAX)'', CAST(10 AS tinyint), CAST(12 AS tinyint)),
    (2, ''2D'', N''Зал 2'', CAST(8 AS tinyint), CAST(10 AS tinyint))');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Format', N'Name', N'Rows', N'SeatsPerRow') AND [object_id] = OBJECT_ID(N'[Halls]'))
        SET IDENTITY_INSERT [Halls] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261126150325_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Genres_Name] ON [Genres] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261126150325_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Genres_Slug] ON [Genres] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261126150325_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Halls_Name] ON [Halls] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261126150325_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MovieGenres_GenreId] ON [MovieGenres] ([GenreId]) INCLUDE ([AddedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261126150325_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MovieGenres_MovieId_Primary] ON [MovieGenres] ([MovieId]) WHERE [IsPrimary] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261126150325_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Movies_Active_Rating] ON [Movies] ([AverageRating] DESC) INCLUDE ([Title], [AgeRating], [BasePrice]) WHERE [IsActive] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261126150325_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Movies_Title_ReleaseYear] ON [Movies] ([Title], [ReleaseYear]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261126150325_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Showtimes_HallId_StartsAt] ON [Showtimes] ([HallId], [StartsAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261126150325_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Showtimes_MovieId_StartsAt] ON [Showtimes] ([MovieId], [StartsAt]) INCLUDE ([HallId], [Price]) WHERE [IsCancelled] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261126150325_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261126150325_InitialCreate', N'10.0.15');
END;

COMMIT;
GO

