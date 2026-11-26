# ERD-діаграми

Діаграми подано двічі: як зображення (`erd-p1.png`, `erd-p2.png`, `cross-db-links.png`) і як код Mermaid, який GitHub теж малює. Той самий опис у форматі DBML для [dbdiagram.io](https://dbdiagram.io) лежить у `docs/erd-p1.dbml` і `docs/erd-p2.dbml`: відкрийте dbdiagram.io → Import → вставте вміст файлу.

Позначення: `PK` первинний ключ, `FK` зовнішній ключ, `UK` унікальний. `||--||` це 1:1, `||--o{` це 1:N, M:N реалізовано через проміжну таблицю.

## Проєкт №1 · CinemaBooking (SQL Server, ADO.NET + Dapper)

![ERD Проєкт №1](erd-p1.png)

```mermaid
erDiagram
    Customers ||--o{ Bookings : "1:N робить"
    Bookings  ||--|| BookingDetails : "1:1 має"
    Bookings  ||--o{ Payments : "1:N оплачується"
    Bookings  ||--o{ Tickets : "M:N (частина 1)"
    Showtimes ||--o{ Tickets : "M:N (частина 2)"

    Customers {
        bigint Id PK "IDENTITY"
        nvarchar Email UK "CHECK формат"
        nvarchar FullName "CHECK не порожнє"
        nvarchar Phone
        datetime2 CreatedAt "DEFAULT SYSUTCDATETIME()"
        nvarchar CreatedBy
        datetime2 UpdatedAt
        nvarchar UpdatedBy
        bit IsDeleted "DEFAULT 0"
        rowversion RowVersion
    }
    Bookings {
        bigint Id PK "IDENTITY"
        bigint CustomerId FK "NO ACTION"
        char BookingCode UK
        nvarchar Status "CHECK, DEFAULT Created"
        decimal TotalAmount "CHECK >= 0"
        datetime2 BookedAt
        datetime2 CreatedAt
        nvarchar CreatedBy
        datetime2 UpdatedAt
        nvarchar UpdatedBy
        bit IsDeleted
        rowversion RowVersion
    }
    BookingDetails {
        bigint BookingId PK, FK "1:1, CASCADE"
        nvarchar Channel "CHECK Web/Mobile/BoxOffice"
        nvarchar ContactPhone
        nvarchar Comment
        nvarchar CancelReason
    }
    Tickets {
        bigint BookingId PK, FK "CASCADE"
        bigint ShowtimeId PK, FK "NO ACTION"
        tinyint SeatRow PK
        tinyint SeatNumber PK
        nvarchar TicketType "CHECK"
        nvarchar MovieTitle "ЗНІМОК"
        datetime2 StartsAt "ЗНІМОК"
        decimal UnitPrice "ЗНІМОК, CHECK > 0"
    }
    Payments {
        bigint Id PK
        bigint BookingId FK
        decimal Amount "CHECK > 0"
        nvarchar Method "CHECK"
        nvarchar Status "CHECK"
        nvarchar ProviderTxnId UK "фільтрований"
        datetime2 PaidAt
        datetime2 CreatedAt
        nvarchar CreatedBy
        datetime2 UpdatedAt
        nvarchar UpdatedBy
        rowversion RowVersion
    }
    Showtimes {
        bigint Id PK "= Id з CinemaCatalog"
        bigint MovieId "Id з Каталогу, БЕЗ FK"
        nvarchar MovieTitle "копія, події"
        nvarchar HallName "копія, події"
        datetime2 StartsAt "копія, події"
        decimal BasePrice "копія, події"
        int Capacity "копія, події"
    }
```

| Зв'язок | Таблиці | Де FK |
|---|---|---|
| 1:1 | Bookings ↔ BookingDetails | `BookingDetails.BookingId` є і PK, і FK |
| 1:N | Customers → Bookings, Bookings → Payments | `Bookings.CustomerId`, `Payments.BookingId` (на боці «багатьох») |
| M:N | Bookings ↔ Showtimes | проміжна `Tickets` (два FK + власні поля: місце, тип, ціна) |

## Проєкт №2 · CinemaCatalog (SQL Server, EF Core)

![ERD Проєкт №2](erd-p2.png)

```mermaid
erDiagram
    Movies ||--|| MovieDetails : "1:1"
    Movies ||--o{ MovieGenres : "M:N (частина 1)"
    Genres ||--o{ MovieGenres : "M:N (частина 2)"
    Movies ||--o{ Showtimes : "1:N"
    Halls  ||--o{ Showtimes : "1:N"

    Genres {
        int Id PK
        nvarchar Name UK
        varchar Slug UK "CHECK a-z0-9-"
    }
    Movies {
        bigint Id PK
        nvarchar Title UK "UK(Title, ReleaseYear)"
        smallint ReleaseYear "CHECK 1895..2100"
        smallint DurationMin "CHECK 1..600"
        varchar AgeRating "CHECK, DEFAULT 0+"
        decimal BasePrice "CHECK > 0"
        bit IsActive "DEFAULT 1"
        decimal AverageRating "копія з Reviews"
        int ReviewsCount "копія з Reviews"
        rowversion RowVersion
    }
    MovieDetails {
        bigint MovieId PK, FK "1:1, CASCADE"
        nvarchar Description
        nvarchar Director
        nvarchar Country
        nvarchar TrailerUrl "CHECK https"
        nvarchar PosterUrl
    }
    MovieGenres {
        bigint MovieId PK, FK "CASCADE"
        int GenreId PK, FK "NO ACTION"
        datetime2 AddedAt "власне поле, DEFAULT now"
        bit IsPrimary "власне поле, UK фільтр."
    }
    Halls {
        int Id PK
        nvarchar Name UK
        tinyint Rows "CHECK"
        tinyint SeatsPerRow "CHECK"
        varchar Format "CHECK 2D/3D/IMAX/4DX"
        int Capacity "обчислюване"
    }
    Showtimes {
        bigint Id PK
        bigint MovieId FK "NO ACTION"
        int HallId FK "NO ACTION"
        datetime2 StartsAt "UK(HallId, StartsAt)"
        decimal Price "CHECK > 0"
        char Language "CHECK uk/en"
        bit IsCancelled
    }
```

| Зв'язок | Таблиці | Де FK |
|---|---|---|
| 1:1 | Movies ↔ MovieDetails | `MovieDetails.MovieId` є і PK, і FK |
| 1:N | Movies → Showtimes, Halls → Showtimes | `Showtimes.MovieId`, `Showtimes.HallId` |
| M:N | Movies ↔ Genres | проміжна `MovieGenres` з власними полями `AddedAt`, `IsPrimary` |

## Логічні посилання між базами (без FOREIGN KEY)

![Посилання між базами](cross-db-links.png)

```mermaid
flowchart LR
    subgraph P2[CinemaCatalog]
        M[Movies.Id]
        S[Showtimes.Id]
    end
    subgraph P1[CinemaBooking]
        BS[Showtimes.Id / MovieId]
        C[Customers.Id]
    end
    subgraph P3[cinema_reviews]
        R[reviews.movieId]
        A[reviews.author.customerId]
    end
    S -. "той самий Id, без FK" .-> BS
    M -. "без FK" .-> R
    C -. "без FK" .-> A
```
