namespace Platform.Aggregator.Clients;

// Мінімальні контракти відповідей сервісів — лише поля, потрібні для картки бронювання.
// System.Text.Json (camelCase, без чутливості до регістру) ігнорує решту полів.

public sealed record BookingTicket(long ShowtimeId, string MovieTitle, DateTime StartsAt, int SeatRow, int SeatNumber,
                                   string TicketType, decimal UnitPrice);

public sealed record BookingResponse(long Id, long CustomerId, string BookingCode, string Status, decimal TotalAmount,
                                     DateTime BookedAt, string Channel, IReadOnlyList<BookingTicket> Tickets);

public sealed record CustomerResponse(long Id, string FullName);

public sealed record ShowtimeResponse(long Id, long MovieId, string MovieTitle, int HallId, string HallName,
                                      DateTime StartsAt, decimal Price, string Language, bool IsCancelled);

public sealed record MovieGenreResponse(string Name, bool IsPrimary);

public sealed record MovieDetailResponse(string? Director, string? Country);

public sealed record MovieResponse(long Id, string Title, short ReleaseYear, short DurationMin, string AgeRating,
                                   MovieDetailResponse? Detail, IReadOnlyList<MovieGenreResponse> Genres);

public sealed record MovieRatingResponse(long MovieId, double Average, int Count);

public sealed record ReviewAuthorResponse(string DisplayName);

public sealed record ReviewItemResponse(string Id, string Type, ReviewAuthorResponse Author, int Rating, string? Text, DateTime CreatedAt);

public sealed record ReviewsPageResponse(IReadOnlyList<ReviewItemResponse> Items, long TotalCount);
