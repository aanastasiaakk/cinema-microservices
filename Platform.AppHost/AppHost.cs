// Оркестрація платформи .NET Aspire: одна команда піднімає бази даних, Redis, три мікросервіси,
// Aggregator і API Gateway.   dotnet run --project Platform.AppHost
// Порядок запуску (WaitFor): БД → сервіси → Aggregator → Gateway.
var builder = DistributedApplication.CreateBuilder(args);

// ---------- Бази даних (контейнери з постійним сховищем) ----------
// Пароль SQL Server — параметр-секрет (user-secrets AppHost: Parameters:sql-password), а не текст у коді.
var sqlPassword = builder.AddParameter("sql-password", secret: true);
var sql = builder.AddSqlServer("sql", sqlPassword)
                 .WithDataVolume("cinema-sql-data")
                 .WithLifetime(ContainerLifetime.Persistent);

// Імена баз = імена рядків підключення в appsettings сервісів (ConnectionStrings:<ім'я>)
var bookingDb = sql.AddDatabase("CinemaBooking");             // Лаб. 02
var catalogDb = sql.AddDatabase("CinemaCatalog");             // Лаб. 03

// mongo:7 — MongoDB 8.x не стартує на ядрах Linux 6.19–7.0.13 (SERVER-121912) у Docker Desktop
var mongo = builder.AddMongoDB("mongo")
                   .WithImageTag("7")
                   .WithDataVolume("cinema-mongo-data")
                   .WithLifetime(ContainerLifetime.Persistent);
var reviewsDb = mongo.AddDatabase("reviewsdb", databaseName: "cinema_reviews");   // Лаб. 04

// ---------- Redis (Лаб. 06): L2-кеш каталогу і кеш агрегованих відповідей ----------
var redis = builder.AddRedis("redis")
                   .WithDataVolume("cinema-redis-data")
                   .WithLifetime(ContainerLifetime.Persistent);

// ---------- Мікросервіси (ім'я ресурсу = логічне ім'я для service discovery) ----------
var bookings = builder.AddProject<Projects.Bookings_Api>("bookings-api")
       .WithReference(bookingDb).WaitFor(bookingDb)
       .WithEnvironment("Database__ApplyScripts", "true")     // порожня БД -> скрипти db/p1 Лаб. 01
       .WithHttpHealthCheck("/health/ready");                 // Лаб. 07: Dashboard показує стан, WaitFor чекає Healthy

var catalog = builder.AddProject<Projects.Catalog_Api>("catalog-api")
       .WithReference(catalogDb).WaitFor(catalogDb)           // схема — міграцією EF Core
       .WithReference(redis).WaitFor(redis)                   // дворівневий кеш L1 → L2 (Redis)
       .WithHttpHealthCheck("/health/ready");

var reviews = builder.AddProject<Projects.Reviews_WebApi>("reviews-api")
       .WithReference(reviewsDb).WaitFor(reviewsDb)           // індекси й сідінг — сам сервіс
       .WithHttpHealthCheck("/health/ready");

// ---------- Aggregator: опитує сервіси паралельно ----------
var aggregator = builder.AddProject<Projects.Platform_Aggregator>("aggregator")
       .WithReference(bookings).WithReference(catalog).WithReference(reviews)
       .WithReference(redis).WaitFor(redis)                   // кеш агрегованих відповідей (TTL 30 с)
       .WaitFor(bookings).WaitFor(catalog).WaitFor(reviews)   // тепер чекає Healthy, а не лише запущений
       .WithHttpHealthCheck("/health/ready");

// ---------- API Gateway: єдина точка входу на ФІКСОВАНОМУ порту ----------
// 5050, а не 5000: на macOS порт 5000 зайнятий AirPlay Receiver.
builder.AddProject<Projects.Platform_ApiGateway>("gateway")
       .WithHttpEndpoint(port: 5050, name: "http")
       .WithExternalHttpEndpoints()
       .WithReference(bookings).WithReference(catalog).WithReference(reviews)
       .WithReference(aggregator)
       .WaitFor(aggregator)
       .WithHttpHealthCheck("/health/live");

builder.Build().Run();
