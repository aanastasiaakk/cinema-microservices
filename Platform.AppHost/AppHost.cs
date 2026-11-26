// Оркестрація платформи .NET Aspire: одна команда піднімає SQL Server і обидва API.
//   dotnet run --project Platform.AppHost
var builder = DistributedApplication.CreateBuilder(args);

// SQL Server у контейнері (дані зберігаються у volume між запусками).
// Імена ресурсів БД = імена рядків підключення в appsettings.json сервісів,
// тому Aspire передає їх як ConnectionStrings__<ім'я> автоматично (WithReference).
var sql = builder.AddSqlServer("sql")
                 .WithDataVolume("cinema-sql-data")
                 .WithLifetime(ContainerLifetime.Persistent);

// ---------- Проєкт №1: Bookings (ADO.NET + Dapper), БД CinemaBooking ----------
var bookingDb = sql.AddDatabase("CinemaBooking");

builder.AddProject<Projects.Bookings_Api>("bookings-api")
       .WithReference(bookingDb)                         // рядок підключення -> API
       .WaitFor(bookingDb)                               // API стартує, коли БД готова
       .WithEnvironment("Database__ApplyScripts", "true")// порожня БД -> скрипти db/p1 Лаб. 01
       .WithExternalHttpEndpoints();

// ---------- Проєкт №2: Catalog (EF Core Code First), БД CinemaCatalog ----------
// Схему створює сам сервіс міграціями EF Core (MigrateAsync) + ідемпотентний сідінг.
var catalogDb = sql.AddDatabase("CinemaCatalog");

builder.AddProject<Projects.Catalog_Api>("catalog-api")
       .WithReference(catalogDb)
       .WaitFor(catalogDb)
       .WithExternalHttpEndpoints();

builder.Build().Run();
