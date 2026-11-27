using System.Diagnostics;

namespace Bookings.Bll.Diagnostics;

/// <summary>
/// Власні spans бізнес-операцій бронювання. Ім'я починається з "Cinema." — ServiceDefaults
/// підписує OpenTelemetry на всі такі джерела (AddSource("Cinema.*")).
/// </summary>
public static class BookingsTelemetry
{
    public const string SourceName = "Cinema.Bookings";
    public static readonly ActivitySource Source = new(SourceName);
}
