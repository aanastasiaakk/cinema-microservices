namespace Platform.Aggregator.Models;

/// <summary>
/// Композитний DTO під бізнес-сценарій «картка бронювання для глядача»: саме бронювання (bookings-api),
/// для кожного сеансу — фільм і зал (catalog-api) та думка глядачів про фільм (reviews-api).
/// Warnings — що не вдалося отримати; null-поля не серіалізуються (camelCase).
/// </summary>
public sealed record FullBookingDto(
    BookingSummary Booking,
    CustomerSummary? Customer,
    IReadOnlyList<SessionCard> Sessions,
    IReadOnlyList<string> Warnings)
{
    public bool IsPartial => Warnings.Count > 0;
}

public sealed record BookingSummary(long Id, string Code, string Status, decimal TotalAmount, DateTime BookedAt,
                                    string Channel, int TicketsCount);

public sealed record CustomerSummary(long Id, string FullName);

/// <summary>Один сеанс у бронюванні: дані з квитків (знімок) + актуальні дані каталогу + відгуки.</summary>
public sealed record SessionCard(
    long ShowtimeId,
    DateTime StartsAt,
    string MovieTitle,
    string? HallName,
    string? Language,
    bool? IsCancelled,
    IReadOnlyList<SeatInfo> Seats,
    MovieInfo? Movie,
    AudienceOpinion? Audience);

public sealed record SeatInfo(int Row, int Seat, string TicketType, decimal Price);

public sealed record MovieInfo(long Id, string Title, short ReleaseYear, short DurationMin, string AgeRating,
                               string? PrimaryGenre, string? Director);

public sealed record AudienceOpinion(double? AverageRating, int ReviewsCount, IReadOnlyList<ReviewSnippet> LatestReviews);

public sealed record ReviewSnippet(string Id, string Author, int Rating, string? Text, DateTime CreatedAt);
