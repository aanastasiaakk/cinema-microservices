// Оркестрація платформи .NET Aspire: одна команда піднімає SQL Server і API.
//   dotnet run --project Platform.AppHost
var builder = DistributedApplication.CreateBuilder(args);

// SQL Server у контейнері (дані зберігаються у volume між запусками).
// Ім'я ресурсу БД "CinemaBooking" = ім'я рядка підключення в appsettings.json API,
// тому Aspire передає його в API як ConnectionStrings__CinemaBooking автоматично.
var sql = builder.AddSqlServer("sql")
                 .WithDataVolume("cinema-sql-data")
                 .WithLifetime(ContainerLifetime.Persistent);

var bookingDb = sql.AddDatabase("CinemaBooking");

builder.AddProject<Projects.Bookings_Api>("bookings-api")
       .WithReference(bookingDb)                         // рядок підключення -> API
       .WaitFor(bookingDb)                               // API стартує, коли БД готова
       .WithEnvironment("Database__ApplyScripts", "true")// порожня БД -> скрипти db/p1 Лаб. 01
       .WithExternalHttpEndpoints();

builder.Build().Run();
