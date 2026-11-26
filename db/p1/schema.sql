/* =====================================================================
   Проєкт №1 · Контекст «Бронювання квитків» (Booking)
   СУБД: SQL Server · доступ у Лаб. 02: ADO.NET + Dapper
   БД: CinemaBooking
   Скрипт ідемпотентний: повторний запуск нічого не ламає і не дублює.

   Зв'язки всередині БД:
     1:1  Bookings  <-> BookingDetails   (FK = PK залежної таблиці)
     1:N  Customers  -> Bookings         (FK на боці «багатьох»)
     1:N  Bookings   -> Payments
     M:N  Bookings  <-> Showtimes        через Tickets (два FK + власні поля)

   Аудит / м'яке видалення / конкурентність:
     Customers, Bookings — CreatedAt/By, UpdatedAt/By, IsDeleted, RowVersion
     Payments            — CreatedAt/By, UpdatedAt/By, RowVersion (без IsDeleted:
                           фінансові записи не видаляються)
     BookingDetails      — частина агрегату Booking: змінюється лише разом із
                           Bookings, тому аудит і RowVersion беруться з Bookings
     Tickets             — незмінний знімок (лише CreatedAt): після додавання не
                           редагується, паралельного запису немає
     Showtimes           — копія з Каталогу: SyncedAt, пише лише обробник подій

   Посилання на інші контексти (БЕЗ FOREIGN KEY між базами):
     Showtimes.Id       = Id сеансу з БД CinemaCatalog (Проєкт №2)
     Showtimes.MovieId  = Id фільму з БД CinemaCatalog — звичайний стовпець
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;   -- обов'язково для фільтрованих індексів (sqlcmd за замовчуванням OFF)
GO

IF DB_ID(N'CinemaBooking') IS NULL
    CREATE DATABASE CinemaBooking;
GO
USE CinemaBooking;
GO

/* ---------------------------------------------------------------------
   Customers — клієнти кінотеатру. Власник даних: контекст Booking.
   PK: BIGINT IDENTITY — записи створюються лише в цій БД, послідовна
   вставка з одного джерела, компактний і швидкий кластерний індекс.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL
CREATE TABLE dbo.Customers (
    Id          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
    Email       NVARCHAR(256) NOT NULL,
    FullName    NVARCHAR(200) NOT NULL,
    Phone       NVARCHAR(20)  NULL,
    -- аудит + м'яке видалення + оптимістична конкурентність
    CreatedAt   DATETIME2(3)  NOT NULL CONSTRAINT DF_Customers_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy   NVARCHAR(100) NULL,
    UpdatedAt   DATETIME2(3)  NULL,
    UpdatedBy   NVARCHAR(100) NULL,
    IsDeleted   BIT           NOT NULL CONSTRAINT DF_Customers_IsDeleted DEFAULT 0,
    RowVersion  ROWVERSION    NOT NULL,   -- профіль може редагувати і клієнт, і адміністратор
    CONSTRAINT UQ_Customers_Email UNIQUE (Email),
    CONSTRAINT CK_Customers_Email CHECK (Email LIKE N'%_@_%._%'),
    CONSTRAINT CK_Customers_FullName CHECK (LEN(LTRIM(RTRIM(FullName))) > 0)
);
GO

/* ---------------------------------------------------------------------
   Showtimes — ЛОКАЛЬНА КОПІЯ довідника сеансів з Каталогу (Проєкт №2).
   Id НЕ IDENTITY: це той самий Id, що в CinemaCatalog.dbo.Showtimes.
   MovieTitle, HallName, StartsAt, BasePrice, Capacity — копія для читання,
   оновлюється подіями з Каталогу (ShowtimeChanged, Лаб. 08).
   Сам Booking ці поля НЕ редагує (немає dual write).
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Showtimes', N'U') IS NULL
CREATE TABLE dbo.Showtimes (
    Id          BIGINT        NOT NULL CONSTRAINT PK_Showtimes PRIMARY KEY,
    MovieId     BIGINT        NOT NULL,   -- посилання на Каталог, БЕЗ FK
    MovieTitle  NVARCHAR(200) NOT NULL,   -- копія з Каталогу (оновлюється подіями)
    HallName    NVARCHAR(100) NOT NULL,   -- копія з Каталогу (оновлюється подіями)
    StartsAt    DATETIME2(0)  NOT NULL,   -- копія з Каталогу (оновлюється подіями)
    BasePrice   DECIMAL(10,2) NOT NULL,   -- копія з Каталогу (оновлюється подіями)
    Capacity    INT           NOT NULL,   -- копія з Каталогу (місткість залу)
    SyncedAt    DATETIME2(3)  NOT NULL CONSTRAINT DF_Showtimes_SyncedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Showtimes_BasePrice CHECK (BasePrice > 0),
    CONSTRAINT CK_Showtimes_Capacity  CHECK (Capacity > 0)
);
GO

/* ---------------------------------------------------------------------
   Bookings — бронювання (корінь агрегату Booking = Bookings + BookingDetails
   + Tickets + Payments). Статуси та переходи:
     Created -> AwaitingPayment -> Paid -> CheckedIn
     Cancelled — лише з Created або AwaitingPayment (до оплати).
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Bookings', N'U') IS NULL
CREATE TABLE dbo.Bookings (
    Id           BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Bookings PRIMARY KEY,
    -- 1:N Customers -> Bookings. ON DELETE NO ACTION: клієнта з історією
    -- бронювань видаляти не можна (лише м'яке видалення).
    CustomerId   BIGINT        NOT NULL
        CONSTRAINT FK_Bookings_Customers REFERENCES dbo.Customers(Id) ON DELETE NO ACTION,
    BookingCode  CHAR(8)       NOT NULL,   -- код для каси / QR
    Status       NVARCHAR(30)  NOT NULL CONSTRAINT DF_Bookings_Status DEFAULT N'Created',
    TotalAmount  DECIMAL(10,2) NOT NULL CONSTRAINT DF_Bookings_TotalAmount DEFAULT 0,
    BookedAt     DATETIME2(3)  NOT NULL CONSTRAINT DF_Bookings_BookedAt DEFAULT SYSUTCDATETIME(),
    CreatedAt    DATETIME2(3)  NOT NULL CONSTRAINT DF_Bookings_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy    NVARCHAR(100) NULL,
    UpdatedAt    DATETIME2(3)  NULL,
    UpdatedBy    NVARCHAR(100) NULL,
    IsDeleted    BIT           NOT NULL CONSTRAINT DF_Bookings_IsDeleted DEFAULT 0,
    RowVersion   ROWVERSION    NOT NULL,   -- статус змінюють касир, платіжний сервіс і клієнт одночасно
    CONSTRAINT UQ_Bookings_BookingCode UNIQUE (BookingCode),
    CONSTRAINT CK_Bookings_Status CHECK (Status IN
        (N'Created', N'AwaitingPayment', N'Paid', N'CheckedIn', N'Cancelled')),
    CONSTRAINT CK_Bookings_TotalAmount CHECK (TotalAmount >= 0)
);
GO

/* ---------------------------------------------------------------------
   BookingDetails — 1:1 з Bookings. FK одночасно є PK.
   ON DELETE CASCADE: деталі не існують без бронювання.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.BookingDetails', N'U') IS NULL
CREATE TABLE dbo.BookingDetails (
    BookingId      BIGINT        NOT NULL CONSTRAINT PK_BookingDetails PRIMARY KEY
        CONSTRAINT FK_BookingDetails_Bookings REFERENCES dbo.Bookings(Id) ON DELETE CASCADE,
    Channel        NVARCHAR(20)  NOT NULL CONSTRAINT DF_BookingDetails_Channel DEFAULT N'Web',
    ContactPhone   NVARCHAR(20)  NULL,
    Comment        NVARCHAR(500) NULL,
    CancelReason   NVARCHAR(300) NULL,
    CONSTRAINT CK_BookingDetails_Channel CHECK (Channel IN (N'Web', N'Mobile', N'BoxOffice'))
);
GO

/* ---------------------------------------------------------------------
   Tickets — M:N Bookings <-> Showtimes (проміжна таблиця з двома FK
   і власними полями: місце, ціна, знімок назви фільму).
   MovieTitle, UnitPrice, StartsAt — ЗНІМОК на момент бронювання,
   свідомо НЕ оновлюється (як OrderItem.ProductName/UnitPrice в eShop).
   ON DELETE CASCADE від Bookings: квитки — частина агрегату бронювання.
   Від Showtimes — NO ACTION: сеанс з проданими квитками не видаляється.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Tickets', N'U') IS NULL
CREATE TABLE dbo.Tickets (
    BookingId   BIGINT        NOT NULL
        CONSTRAINT FK_Tickets_Bookings  REFERENCES dbo.Bookings(Id)  ON DELETE CASCADE,
    ShowtimeId  BIGINT        NOT NULL
        CONSTRAINT FK_Tickets_Showtimes REFERENCES dbo.Showtimes(Id) ON DELETE NO ACTION,
    SeatRow     TINYINT       NOT NULL,
    SeatNumber  TINYINT       NOT NULL,
    TicketType  NVARCHAR(20)  NOT NULL CONSTRAINT DF_Tickets_TicketType DEFAULT N'Adult',
    MovieTitle  NVARCHAR(200) NOT NULL,   -- знімок, не оновлюється
    StartsAt    DATETIME2(0)  NOT NULL,   -- знімок, не оновлюється
    UnitPrice   DECIMAL(10,2) NOT NULL,   -- знімок ціни з урахуванням типу квитка
    CreatedAt   DATETIME2(3)  NOT NULL CONSTRAINT DF_Tickets_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Tickets PRIMARY KEY (BookingId, ShowtimeId, SeatRow, SeatNumber),
    CONSTRAINT CK_Tickets_Seat       CHECK (SeatRow > 0 AND SeatNumber > 0),
    CONSTRAINT CK_Tickets_UnitPrice  CHECK (UnitPrice > 0),
    CONSTRAINT CK_Tickets_TicketType CHECK (TicketType IN (N'Adult', N'Student', N'Child'))
);
GO

/* ---------------------------------------------------------------------
   Payments — 1:N Bookings -> Payments (можуть бути невдалі спроби).
   Фінансовий запис: фізично і м'яко НЕ видаляється (IsDeleted немає свідомо),
   помилкові оплати переводяться в статус Refunded.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Payments', N'U') IS NULL
CREATE TABLE dbo.Payments (
    Id             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Payments PRIMARY KEY,
    BookingId      BIGINT        NOT NULL
        CONSTRAINT FK_Payments_Bookings REFERENCES dbo.Bookings(Id) ON DELETE NO ACTION,
    Amount         DECIMAL(10,2) NOT NULL,
    Method         NVARCHAR(20)  NOT NULL,
    Status         NVARCHAR(20)  NOT NULL CONSTRAINT DF_Payments_Status DEFAULT N'Pending',
    ProviderTxnId  NVARCHAR(100) NULL,
    PaidAt         DATETIME2(3)  NULL,
    CreatedAt      DATETIME2(3)  NOT NULL CONSTRAINT DF_Payments_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy      NVARCHAR(100) NULL,
    UpdatedAt      DATETIME2(3)  NULL,
    UpdatedBy      NVARCHAR(100) NULL,
    -- статус оплати одночасно може змінити callback провайдера і таймаут/повернення
    RowVersion     ROWVERSION    NOT NULL,
    CONSTRAINT CK_Payments_Amount CHECK (Amount > 0),
    CONSTRAINT CK_Payments_Method CHECK (Method IN (N'Card', N'Cash', N'ApplePay', N'GooglePay')),
    CONSTRAINT CK_Payments_Status CHECK (Status IN (N'Pending', N'Succeeded', N'Failed', N'Refunded'))
);
GO

/* =====================================================================
   ІНДЕКСИ — кожен під конкретний запит
   ===================================================================== */

-- Запит: «мої бронювання» — бронювання клієнта з певним статусом,
-- лише не видалені, новіші першими. Фільтрований + покриваючий.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Bookings_Customer_Status_Active')
CREATE INDEX IX_Bookings_Customer_Status_Active
    ON dbo.Bookings (CustomerId, Status, BookedAt DESC)
    INCLUDE (BookingCode, TotalAmount)
    WHERE IsDeleted = 0;
GO

-- Запит: фонова задача «скасувати неоплачені бронювання старші за 15 хв».
-- Фільтрований індекс лише по рядках зі статусом AwaitingPayment.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Bookings_AwaitingPayment_BookedAt')
CREATE INDEX IX_Bookings_AwaitingPayment_BookedAt
    ON dbo.Bookings (BookedAt)
    WHERE Status = N'AwaitingPayment' AND IsDeleted = 0;
GO

-- Запит: «зайняті місця на сеансі» (схема залу) і перевірка в
-- usp_Booking_AddTicket, що місце вільне. Покриваючий: BookingId для JOIN зі статусом.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tickets_Showtime_Seat')
CREATE INDEX IX_Tickets_Showtime_Seat
    ON dbo.Tickets (ShowtimeId, SeatRow, SeatNumber)
    INCLUDE (BookingId);
GO

-- Запит: «історія оплат бронювання» та перевірка наявності успішної оплати
-- при переході в Paid (usp_Booking_ChangeStatus).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Payments_Booking_Status')
CREATE INDEX IX_Payments_Booking_Status
    ON dbo.Payments (BookingId, Status)
    INCLUDE (Amount, PaidAt);
GO

-- Запит: розклад «сеанси на дату» для каси (локальна копія).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Showtimes_StartsAt')
CREATE INDEX IX_Showtimes_StartsAt
    ON dbo.Showtimes (StartsAt)
    INCLUDE (MovieTitle, HallName, BasePrice);
GO

-- Запит: пошук провайдером платежу за його ідентифікатором транзакції
-- (callback від платіжної системи). Унікальний, лише для непорожніх.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Payments_ProviderTxnId')
CREATE UNIQUE INDEX UX_Payments_ProviderTxnId
    ON dbo.Payments (ProviderTxnId)
    WHERE ProviderTxnId IS NOT NULL;
GO

/* ---------------------------------------------------------------------
   Представлення для читання (денормалізоване): бронювання + клієнт + квитки.
   --------------------------------------------------------------------- */
CREATE OR ALTER VIEW dbo.vw_BookingSummary
AS
SELECT b.Id            AS BookingId,
       b.BookingCode,
       b.Status,
       b.BookedAt,
       b.TotalAmount,
       c.Id            AS CustomerId,
       c.FullName      AS CustomerName,
       c.Email         AS CustomerEmail,
       COUNT(t.ShowtimeId) AS TicketsCount
FROM dbo.Bookings b
JOIN dbo.Customers c   ON c.Id = b.CustomerId
LEFT JOIN dbo.Tickets t ON t.BookingId = b.Id
WHERE b.IsDeleted = 0
GROUP BY b.Id, b.BookingCode, b.Status, b.BookedAt, b.TotalAmount,
         c.Id, c.FullName, c.Email;
GO
