/* =====================================================================
   Проєкт №1 · CinemaBooking · Тестові дані
   ІДЕМПОТЕНТНО: кожна вставка перевіряє наявність запису за природним
   ключем (Email, Id сеансу, BookingCode, ProviderTxnId), тому повторний
   запуск не створює дублікатів.
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;   -- обов'язково для фільтрованих індексів (sqlcmd за замовчуванням OFF)
GO
USE CinemaBooking;
GO
SET NOCOUNT ON;

/* ---------- Клієнти (find-or-create за Email) ---------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE Email = N'olena.koval@example.com')
    INSERT INTO dbo.Customers (Email, FullName, Phone, CreatedBy)
    VALUES (N'olena.koval@example.com', N'Олена Коваль', N'+380501112233', N'seed');

IF NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE Email = N'andrii.melnyk@example.com')
    INSERT INTO dbo.Customers (Email, FullName, Phone, CreatedBy)
    VALUES (N'andrii.melnyk@example.com', N'Андрій Мельник', N'+380672223344', N'seed');

IF NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE Email = N'iryna.bondar@example.com')
    INSERT INTO dbo.Customers (Email, FullName, Phone, CreatedBy)
    VALUES (N'iryna.bondar@example.com', N'Ірина Бондар', NULL, N'seed');
GO

/* ---------- Локальна копія сеансів з Каталогу ----------
   Id і значення збігаються з db/p2/seed.sql (CinemaCatalog.dbo.Showtimes).
   У робочій системі ці рядки приходять подіями ShowtimeScheduled/Changed. */
MERGE dbo.Showtimes AS target
USING (VALUES
    (1, 1, N'Довбуш',              N'Зал 1 (IMAX)', CAST('2027-01-15T18:00:00' AS DATETIME2(0)), 220.00, 120),
    (2, 1, N'Довбуш',              N'Зал 2',        CAST('2027-01-15T21:00:00' AS DATETIME2(0)), 180.00,  80),
    (3, 2, N'Мавка. Лісова пісня', N'Зал 2',        CAST('2027-01-16T12:00:00' AS DATETIME2(0)), 150.00,  80),
    (4, 3, N'Дюна: Частина друга', N'Зал 1 (IMAX)', CAST('2027-01-16T19:30:00' AS DATETIME2(0)), 250.00, 120)
) AS src (Id, MovieId, MovieTitle, HallName, StartsAt, BasePrice, Capacity)
ON target.Id = src.Id
WHEN NOT MATCHED THEN
    INSERT (Id, MovieId, MovieTitle, HallName, StartsAt, BasePrice, Capacity)
    VALUES (src.Id, src.MovieId, src.MovieTitle, src.HallName, src.StartsAt, src.BasePrice, src.Capacity);
GO

/* ---------- Бронювання (find-or-create за BookingCode) ---------- */
DECLARE @Olena  BIGINT = (SELECT Id FROM dbo.Customers WHERE Email = N'olena.koval@example.com');
DECLARE @Andrii BIGINT = (SELECT Id FROM dbo.Customers WHERE Email = N'andrii.melnyk@example.com');
DECLARE @Iryna  BIGINT = (SELECT Id FROM dbo.Customers WHERE Email = N'iryna.bondar@example.com');

-- B1: оплачене бронювання Олени (2 квитки на «Довбуш», IMAX)
IF NOT EXISTS (SELECT 1 FROM dbo.Bookings WHERE BookingCode = 'SEED0001')
BEGIN
    INSERT INTO dbo.Bookings (CustomerId, BookingCode, Status, TotalAmount, CreatedBy)
    VALUES (@Olena, 'SEED0001', N'Paid', 440.00, N'seed');
    DECLARE @B1 BIGINT = SCOPE_IDENTITY();

    INSERT INTO dbo.BookingDetails (BookingId, Channel, ContactPhone, Comment)
    VALUES (@B1, N'Web', N'+380501112233', N'Місця поруч');

    INSERT INTO dbo.Tickets (BookingId, ShowtimeId, SeatRow, SeatNumber, TicketType, MovieTitle, StartsAt, UnitPrice)
    VALUES (@B1, 1, 5, 7, N'Adult', N'Довбуш', '2027-01-15T18:00:00', 220.00),
           (@B1, 1, 5, 8, N'Adult', N'Довбуш', '2027-01-15T18:00:00', 220.00);
END

-- B2: Андрій, очікує оплати (студентський квиток на «Мавку» + звичайний на «Дюну»)
IF NOT EXISTS (SELECT 1 FROM dbo.Bookings WHERE BookingCode = 'SEED0002')
BEGIN
    INSERT INTO dbo.Bookings (CustomerId, BookingCode, Status, TotalAmount, CreatedBy)
    VALUES (@Andrii, 'SEED0002', N'AwaitingPayment', 370.00, N'seed');
    DECLARE @B2 BIGINT = SCOPE_IDENTITY();

    INSERT INTO dbo.BookingDetails (BookingId, Channel)
    VALUES (@B2, N'Mobile');

    INSERT INTO dbo.Tickets (BookingId, ShowtimeId, SeatRow, SeatNumber, TicketType, MovieTitle, StartsAt, UnitPrice)
    VALUES (@B2, 3, 3, 4, N'Student', N'Мавка. Лісова пісня', '2027-01-16T12:00:00', 120.00),
           (@B2, 4, 8, 10, N'Adult',  N'Дюна: Частина друга', '2027-01-16T19:30:00', 250.00);
END

-- B3: Ірина, нове бронювання без квитків (для демонстрації процедур)
IF NOT EXISTS (SELECT 1 FROM dbo.Bookings WHERE BookingCode = 'SEED0003')
BEGIN
    INSERT INTO dbo.Bookings (CustomerId, BookingCode, Status, CreatedBy)
    VALUES (@Iryna, 'SEED0003', N'Created', N'seed');

    INSERT INTO dbo.BookingDetails (BookingId, Channel)
    VALUES (SCOPE_IDENTITY(), N'BoxOffice');
END

-- B4: скасоване бронювання Андрія
IF NOT EXISTS (SELECT 1 FROM dbo.Bookings WHERE BookingCode = 'SEED0004')
BEGIN
    INSERT INTO dbo.Bookings (CustomerId, BookingCode, Status, TotalAmount, CreatedBy)
    VALUES (@Andrii, 'SEED0004', N'Cancelled', 180.00, N'seed');
    DECLARE @B4 BIGINT = SCOPE_IDENTITY();

    INSERT INTO dbo.BookingDetails (BookingId, Channel, CancelReason)
    VALUES (@B4, N'Web', N'Змінилися плани');

    INSERT INTO dbo.Tickets (BookingId, ShowtimeId, SeatRow, SeatNumber, TicketType, MovieTitle, StartsAt, UnitPrice)
    VALUES (@B4, 2, 1, 1, N'Adult', N'Довбуш', '2027-01-15T21:00:00', 180.00);
END
GO

/* ---------- Оплати (find-or-create за ProviderTxnId) ---------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Payments WHERE ProviderTxnId = N'TXN-SEED-0001')
    INSERT INTO dbo.Payments (BookingId, Amount, Method, Status, ProviderTxnId, PaidAt, CreatedBy)
    SELECT Id, 440.00, N'Card', N'Succeeded', N'TXN-SEED-0001', SYSUTCDATETIME(), N'seed'
    FROM dbo.Bookings WHERE BookingCode = 'SEED0001';

IF NOT EXISTS (SELECT 1 FROM dbo.Payments WHERE ProviderTxnId = N'TXN-SEED-0002')
    INSERT INTO dbo.Payments (BookingId, Amount, Method, Status, ProviderTxnId, CreatedBy)
    SELECT Id, 370.00, N'GooglePay', N'Failed', N'TXN-SEED-0002', N'seed'
    FROM dbo.Bookings WHERE BookingCode = 'SEED0002';
GO

PRINT N'Seed CinemaBooking: OK';
GO
