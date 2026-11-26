/* =====================================================================
   Проєкт №1 · CinemaBooking · Запити перевірки (CRUD, пошук, пагінація,
   транзакційні сценарії, самоперевірка перед здачею).
   Запускати після schema.sql, procedures.sql, seed.sql.
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;   -- обов'язково для фільтрованих індексів (sqlcmd за замовчуванням OFF)
GO
USE CinemaBooking;
GO

/* 1. Самоперевірка: усі FK посилаються лише на таблиці цієї ж БД */
SELECT fk.name                                   AS ForeignKey,
       OBJECT_NAME(fk.parent_object_id)          AS FromTable,
       OBJECT_NAME(fk.referenced_object_id)      AS ToTable,
       fk.delete_referential_action_desc         AS OnDelete
FROM sys.foreign_keys fk
ORDER BY FromTable;
GO

/* 2. Індекси та їхні фільтри */
SELECT OBJECT_NAME(i.object_id) AS TableName, i.name AS IndexName,
       i.type_desc, i.is_unique, i.filter_definition
FROM sys.indexes i
WHERE OBJECTPROPERTY(i.object_id, 'IsUserTable') = 1 AND i.name IS NOT NULL
ORDER BY TableName, IndexName;
GO

/* 3. CRUD: створити клієнта, прочитати, оновити, м'яко видалити бронювання */
DECLARE @Email NVARCHAR(256) = CONCAT(N'demo', ABS(CHECKSUM(NEWID())) % 100000, N'@example.com');
DECLARE @Ids TABLE (Id BIGINT);
INSERT INTO @Ids EXEC dbo.usp_Customer_Upsert @Email = @Email, @FullName = N'Демо Клієнт';
DECLARE @CustomerId BIGINT = (SELECT Id FROM @Ids);

DECLARE @Rv BINARY(8) = (SELECT RowVersion FROM dbo.Customers WHERE Id = @CustomerId);
EXEC dbo.usp_Customer_Upsert @Id = @CustomerId, @Email = @Email,
     @FullName = N'Демо Клієнт (оновлено)', @RowVersion = @Rv;

-- той самий старий RowVersion -> конфлікт конкурентності (50011)
BEGIN TRY
    EXEC dbo.usp_Customer_Upsert @Id = @CustomerId, @Email = @Email,
         @FullName = N'Інша зміна', @RowVersion = @Rv;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    SELECT ERROR_NUMBER() AS ErrorNumber, ERROR_MESSAGE() AS ExpectedError;
END CATCH;

/* 4. Бізнес-сценарій: створити бронювання -> додати квитки -> до оплати */
DECLARE @B TABLE (Id BIGINT, BookingCode CHAR(8));
INSERT INTO @B EXEC dbo.usp_Booking_Create @CustomerId = @CustomerId, @Channel = N'Web';
DECLARE @BookingId BIGINT = (SELECT Id FROM @B);

DECLARE @Seat TINYINT = (SELECT ISNULL(MAX(SeatNumber), 0) + 1
                         FROM dbo.Tickets WHERE ShowtimeId = 4 AND SeatRow = 10);
EXEC dbo.usp_Booking_AddTicket @BookingId = @BookingId, @ShowtimeId = 4,
     @SeatRow = 10, @SeatNumber = @Seat, @TicketType = N'Student';

-- те саме місце ще раз -> «Seat is already taken» (50033)
BEGIN TRY
    EXEC dbo.usp_Booking_AddTicket @BookingId = @BookingId, @ShowtimeId = 4,
         @SeatRow = 10, @SeatNumber = @Seat;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    SELECT ERROR_NUMBER() AS ErrorNumber, ERROR_MESSAGE() AS ExpectedError;
END CATCH;

EXEC dbo.usp_Booking_ChangeStatus @BookingId = @BookingId, @NewStatus = N'AwaitingPayment';

/* 5. САМОПЕРЕВІРКА: недопустимий перехід -> керована помилка, а не дія.
      «Видати квиток на вхід» (CheckedIn) неоплаченому бронюванню. */
BEGIN TRY
    EXEC dbo.usp_Booking_ChangeStatus @BookingId = @BookingId, @NewStatus = N'CheckedIn';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    SELECT ERROR_NUMBER() AS ErrorNumber, ERROR_MESSAGE() AS ExpectedError;
END CATCH;

-- спроба перевести в Paid без успішної оплати -> 50041
BEGIN TRY
    EXEC dbo.usp_Booking_ChangeStatus @BookingId = @BookingId, @NewStatus = N'Paid';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    SELECT ERROR_NUMBER() AS ErrorNumber, ERROR_MESSAGE() AS ExpectedError;
END CATCH;

-- статус не змінився
SELECT Id, Status FROM dbo.Bookings WHERE Id = @BookingId;

-- скасування до оплати дозволене, далі м'яке видалення
EXEC dbo.usp_Booking_ChangeStatus @BookingId = @BookingId, @NewStatus = N'Cancelled',
     @Reason = N'Демо-скасування';
EXEC dbo.usp_Booking_SoftDelete @BookingId = @BookingId;
GO

/* 6. Скасувати ОПЛАЧЕНЕ бронювання не можна (Paid -> Cancelled) */
DECLARE @PaidId BIGINT = (SELECT Id FROM dbo.Bookings WHERE BookingCode = 'SEED0001');
BEGIN TRY
    EXEC dbo.usp_Booking_ChangeStatus @BookingId = @PaidId, @NewStatus = N'Cancelled';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    SELECT ERROR_NUMBER() AS ErrorNumber, ERROR_MESSAGE() AS ExpectedError;
END CATCH;
GO

/* 7. Читання агрегату */
DECLARE @Id BIGINT = (SELECT Id FROM dbo.Bookings WHERE BookingCode = 'SEED0002');
EXEC dbo.usp_Booking_GetById @BookingId = @Id;
GO

/* 8. Пошук + пагінація: бронювання клієнта, сторінка 1 по 10
      (використовує IX_Bookings_Customer_Status_Active) */
DECLARE @CustomerId BIGINT = (SELECT Id FROM dbo.Customers WHERE Email = N'andrii.melnyk@example.com');
DECLARE @Page INT = 1, @PageSize INT = 10;
SELECT Id, BookingCode, Status, BookedAt, TotalAmount
FROM dbo.Bookings
WHERE CustomerId = @CustomerId AND IsDeleted = 0
ORDER BY BookedAt DESC
OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

/* 9. Зайняті місця на сеансі (IX_Tickets_Showtime_Seat) */
SELECT t.SeatRow, t.SeatNumber, b.Status
FROM dbo.Tickets t
JOIN dbo.Bookings b ON b.Id = t.BookingId
WHERE t.ShowtimeId = 1 AND b.Status <> N'Cancelled' AND b.IsDeleted = 0
ORDER BY t.SeatRow, t.SeatNumber;

/* 10. Денормалізоване представлення для читання */
SELECT * FROM dbo.vw_BookingSummary ORDER BY BookingId;
GO
