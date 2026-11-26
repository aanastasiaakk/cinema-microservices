namespace Bookings.Tests;

/// <summary>
/// Інтеграційні тести запускаються проти справжньої БД CinemaBooking (Лаб. 01), лише якщо задано
/// змінну оточення BOOKINGS_TEST_DB з рядком підключення. Інакше вони пропускаються.
/// </summary>
public static class TestDb
{
    public static string? ConnectionString => Environment.GetEnvironmentVariable("BOOKINGS_TEST_DB");
    public const string SkipReason = "Set BOOKINGS_TEST_DB to run integration tests against SQL Server";
}

public sealed class DbFactAttribute : FactAttribute
{
    public DbFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(TestDb.ConnectionString)) Skip = TestDb.SkipReason;
    }
}
