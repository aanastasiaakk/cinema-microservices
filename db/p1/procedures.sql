/* =====================================================================
   Проєкт №1 · CinemaBooking · Збережувані процедури (6 шт.)
   Їх викликатиме DAL у Лаб. 02 (ADO.NET + Dapper).

   CRUD:
     1. usp_Customer_Upsert      — створити / оновити клієнта (з перевіркою RowVersion)
     2. usp_Booking_GetById      — прочитати бронювання з квитками і оплатами
     3. usp_Booking_SoftDelete   — м'яке видалення бронювання
   Бізнес-операції з транзакціями:
     4. usp_Booking_Create       — створити бронювання + деталі (1:1) в одній транзакції
     5. usp_Booking_AddTicket    — додати квиток: перевірка місця, знімок назви й ціни
     6. usp_Booking_ChangeStatus — зміна статусу з перевіркою допустимого переходу

   Коди керованих помилок (THROW): 50001–50099.
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;   -- обов'язково для фільтрованих індексів (sqlcmd за замовчуванням OFF)
GO
USE CinemaBooking;
GO

/* ---------------------------------------------------------------------
   1. usp_Customer_Upsert
   @Id IS NULL  -> INSERT, повертає новий Id.
   @Id NOT NULL -> UPDATE з оптимістичною конкурентністю: якщо @RowVersion
                   не збігається, хтось уже змінив запис -> помилка 50011.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_Customer_Upsert
    @Id         BIGINT        = NULL,
    @Email      NVARCHAR(256),
    @FullName   NVARCHAR(200),
    @Phone      NVARCHAR(20)  = NULL,
    @RowVersion BINARY(8)     = NULL,
    @User       NVARCHAR(100) = N'system'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Id IS NULL
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.Customers WHERE Email = @Email)
            THROW 50010, N'Customer with this email already exists', 1;

        INSERT INTO dbo.Customers (Email, FullName, Phone, CreatedBy)
        VALUES (@Email, @FullName, @Phone, @User);

        SELECT CAST(SCOPE_IDENTITY() AS BIGINT) AS Id;
        RETURN;
    END

    UPDATE dbo.Customers
    SET Email     = @Email,
        FullName  = @FullName,
        Phone     = @Phone,
        UpdatedAt = SYSUTCDATETIME(),
        UpdatedBy = @User
    WHERE Id = @Id
      AND IsDeleted = 0
      AND (@RowVersion IS NULL OR RowVersion = @RowVersion);

    IF @@ROWCOUNT = 0
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE Id = @Id AND IsDeleted = 0)
            THROW 50012, N'Customer not found', 1;
        THROW 50011, N'Concurrency conflict: customer was modified by another user', 1;
    END

    SELECT @Id AS Id;
END
GO

/* ---------------------------------------------------------------------
   2. usp_Booking_GetById — три набори результатів:
      бронювання (+деталі, 1:1), квитки (M:N), оплати (1:N).
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_Booking_GetById
    @BookingId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT b.Id, b.BookingCode, b.CustomerId, c.FullName AS CustomerName,
           b.Status, b.TotalAmount, b.BookedAt, b.RowVersion,
           d.Channel, d.ContactPhone, d.Comment, d.CancelReason
    FROM dbo.Bookings b
    JOIN dbo.Customers c            ON c.Id = b.CustomerId
    LEFT JOIN dbo.BookingDetails d  ON d.BookingId = b.Id
    WHERE b.Id = @BookingId AND b.IsDeleted = 0;

    SELECT t.ShowtimeId, t.MovieTitle, t.StartsAt, t.SeatRow, t.SeatNumber,
           t.TicketType, t.UnitPrice
    FROM dbo.Tickets t
    JOIN dbo.Bookings b ON b.Id = t.BookingId
    WHERE t.BookingId = @BookingId AND b.IsDeleted = 0
    ORDER BY t.StartsAt, t.SeatRow, t.SeatNumber;

    SELECT p.Id, p.Amount, p.Method, p.Status, p.ProviderTxnId, p.PaidAt
    FROM dbo.Payments p
    JOIN dbo.Bookings b ON b.Id = p.BookingId
    WHERE p.BookingId = @BookingId AND b.IsDeleted = 0
    ORDER BY p.CreatedAt;
END
GO

/* ---------------------------------------------------------------------
   3. usp_Booking_SoftDelete — м'яке видалення (IsDeleted = 1).
      Видаляти можна лише скасовані бронювання: оплачені потрібні для звітності.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_Booking_SoftDelete
    @BookingId BIGINT,
    @User      NVARCHAR(100) = N'system'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Status NVARCHAR(30) =
        (SELECT Status FROM dbo.Bookings WHERE Id = @BookingId AND IsDeleted = 0);

    IF @Status IS NULL
        THROW 50001, N'Booking not found', 1;
    IF @Status <> N'Cancelled'
        THROW 50020, N'Only cancelled bookings can be deleted', 1;

    UPDATE dbo.Bookings
    SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @User
    WHERE Id = @BookingId;
END
GO

/* ---------------------------------------------------------------------
   4. usp_Booking_Create — створює бронювання і його деталі (1:1)
      атомарно в одній явній транзакції. Повертає Id і BookingCode.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_Booking_Create
    @CustomerId   BIGINT,
    @Channel      NVARCHAR(20)  = N'Web',
    @ContactPhone NVARCHAR(20)  = NULL,
    @Comment      NVARCHAR(500) = NULL,
    @User         NVARCHAR(100) = N'system'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE Id = @CustomerId AND IsDeleted = 0)
        THROW 50012, N'Customer not found', 1;

    DECLARE @Code CHAR(8) = UPPER(LEFT(REPLACE(CONVERT(CHAR(36), NEWID()), '-', ''), 8));
    DECLARE @NewId BIGINT;

    BEGIN TRY
        BEGIN TRANSACTION;
            INSERT INTO dbo.Bookings (CustomerId, BookingCode, Status, CreatedBy)
            VALUES (@CustomerId, @Code, N'Created', @User);

            SET @NewId = SCOPE_IDENTITY();

            INSERT INTO dbo.BookingDetails (BookingId, Channel, ContactPhone, Comment)
            VALUES (@NewId, @Channel, @ContactPhone, @Comment);
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        -- відкочуємо, щоб помилка не залишала «завислої» транзакції у викликача
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;   -- передаємо керовану помилку далі (у DAL)
    END CATCH;

    SELECT @NewId AS Id, @Code AS BookingCode;
END
GO

/* ---------------------------------------------------------------------
   5. usp_Booking_AddTicket — додає квиток на сеанс.
      - бронювання має бути в статусі Created;
      - місце не зайняте іншим не скасованим бронюванням;
      - MovieTitle, StartsAt, UnitPrice копіюються з локальної копії Showtimes
        як ЗНІМОК (далі не оновлюються);
      - TotalAmount перераховується в тій самій транзакції.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_Booking_AddTicket
    @BookingId  BIGINT,
    @ShowtimeId BIGINT,
    @SeatRow    TINYINT,
    @SeatNumber TINYINT,
    @TicketType NVARCHAR(20)  = N'Adult',
    @User       NVARCHAR(100) = N'system'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

            DECLARE @Status NVARCHAR(30) =
                (SELECT Status FROM dbo.Bookings WITH (UPDLOCK)
                 WHERE Id = @BookingId AND IsDeleted = 0);

            IF @Status IS NULL
                THROW 50001, N'Booking not found', 1;
            IF @Status <> N'Created'
                THROW 50030, N'Tickets can be added only to a booking in status Created', 1;

            DECLARE @Title NVARCHAR(200), @StartsAt DATETIME2(0), @BasePrice DECIMAL(10,2);
            SELECT @Title = MovieTitle, @StartsAt = StartsAt, @BasePrice = BasePrice
            FROM dbo.Showtimes WHERE Id = @ShowtimeId;

            IF @Title IS NULL
                THROW 50031, N'Showtime not found', 1;
            IF @StartsAt <= SYSUTCDATETIME()
                THROW 50032, N'Showtime has already started', 1;

            -- UPDLOCK + HOLDLOCK: два касири не продадуть одне місце одночасно
            IF EXISTS (
                SELECT 1
                FROM dbo.Tickets t WITH (UPDLOCK, HOLDLOCK)
                JOIN dbo.Bookings b ON b.Id = t.BookingId
                WHERE t.ShowtimeId = @ShowtimeId
                  AND t.SeatRow = @SeatRow AND t.SeatNumber = @SeatNumber
                  AND b.Status <> N'Cancelled' AND b.IsDeleted = 0)
                THROW 50033, N'Seat is already taken', 1;

            DECLARE @Price DECIMAL(10,2) = CASE @TicketType
                WHEN N'Student' THEN ROUND(@BasePrice * 0.8, 2)
                WHEN N'Child'   THEN ROUND(@BasePrice * 0.6, 2)
                ELSE @BasePrice END;

            INSERT INTO dbo.Tickets (BookingId, ShowtimeId, SeatRow, SeatNumber,
                                     TicketType, MovieTitle, StartsAt, UnitPrice)
            VALUES (@BookingId, @ShowtimeId, @SeatRow, @SeatNumber,
                    @TicketType, @Title, @StartsAt, @Price);

            UPDATE dbo.Bookings
            SET TotalAmount = (SELECT SUM(UnitPrice) FROM dbo.Tickets WHERE BookingId = @BookingId),
                UpdatedAt   = SYSUTCDATETIME(),
                UpdatedBy   = @User
            WHERE Id = @BookingId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        -- відкочуємо, щоб помилка не залишала «завислої» транзакції у викликача
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;   -- передаємо керовану помилку далі (у DAL)
    END CATCH;
END
GO

/* ---------------------------------------------------------------------
   6. usp_Booking_ChangeStatus — ТРАНЗАКЦІЙНА зміна статусу.
      Допустимі переходи:
        Created          -> AwaitingPayment   (потрібен хоча б один квиток)
        AwaitingPayment  -> Paid              (потрібна успішна оплата на всю суму)
        Paid             -> CheckedIn         (не раніше ніж за 1 год до сеансу)
        Created          -> Cancelled
        AwaitingPayment  -> Cancelled         (скасування лише ДО оплати)
      Будь-який інший перехід (наприклад, Created -> CheckedIn,
      Paid -> Cancelled) відхиляється керованою помилкою 50002.
      @ExpectedRowVersion (необов'язковий) — оптимістична конкурентність.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_Booking_ChangeStatus
    @BookingId          BIGINT,
    @NewStatus          NVARCHAR(30),
    @Reason             NVARCHAR(300) = NULL,
    @ExpectedRowVersion BINARY(8)     = NULL,
    @User               NVARCHAR(100) = N'system'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;          -- будь-яка помилка відкочує всю транзакцію

    BEGIN TRY
        BEGIN TRANSACTION;

            DECLARE @Status NVARCHAR(30), @RowVersion BINARY(8), @Total DECIMAL(10,2);

            -- UPDLOCK тримає рядок до кінця транзакції: паралельна зміна статусу чекатиме
            SELECT @Status = Status, @RowVersion = RowVersion, @Total = TotalAmount
            FROM dbo.Bookings WITH (UPDLOCK, ROWLOCK)
            WHERE Id = @BookingId AND IsDeleted = 0;

            IF @Status IS NULL
                THROW 50001, N'Booking not found', 1;

            IF @ExpectedRowVersion IS NOT NULL AND @ExpectedRowVersion <> @RowVersion
                THROW 50011, N'Concurrency conflict: booking was modified by another user', 1;

            IF NOT (
                   (@Status = N'Created'         AND @NewStatus IN (N'AwaitingPayment', N'Cancelled'))
                OR (@Status = N'AwaitingPayment' AND @NewStatus IN (N'Paid', N'Cancelled'))
                OR (@Status = N'Paid'            AND @NewStatus = N'CheckedIn'))
            BEGIN
                DECLARE @Msg NVARCHAR(200) =
                    CONCAT(N'Invalid status transition: ', @Status, N' -> ', @NewStatus);
                THROW 50002, @Msg, 1;
            END

            IF @NewStatus = N'AwaitingPayment'
               AND NOT EXISTS (SELECT 1 FROM dbo.Tickets WHERE BookingId = @BookingId)
                THROW 50040, N'Booking has no tickets', 1;

            IF @NewStatus = N'Paid'
               AND ISNULL((SELECT SUM(Amount) FROM dbo.Payments
                           WHERE BookingId = @BookingId AND Status = N'Succeeded'), 0) < @Total
                THROW 50041, N'Booking is not fully paid', 1;

            IF @NewStatus = N'CheckedIn'
               AND EXISTS (SELECT 1 FROM dbo.Tickets
                           WHERE BookingId = @BookingId
                             AND StartsAt > DATEADD(HOUR, 1, SYSUTCDATETIME()))
                THROW 50042, N'Check-in opens 1 hour before the showtime', 1;

            UPDATE dbo.Bookings
            SET Status    = @NewStatus,
                UpdatedAt = SYSUTCDATETIME(),
                UpdatedBy = @User
            WHERE Id = @BookingId;

            IF @NewStatus = N'Cancelled'
                UPDATE dbo.BookingDetails
                SET CancelReason = @Reason
                WHERE BookingId = @BookingId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        -- відкочуємо, щоб помилка не залишала «завислої» транзакції у викликача
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;   -- передаємо керовану помилку далі (у DAL)
    END CATCH;

    SELECT @BookingId AS Id, @Status AS OldStatus, @NewStatus AS NewStatus;
END
GO
