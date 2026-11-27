using Platform.Aggregator.Clients;

namespace Platform.Aggregator.Services;

/// <summary>
/// Звідки Aggregator бере дані для картки бронювання. Два транспорти з однаковою семантикою:
/// gRPC (за замовчуванням, Лаб. 06) і HTTP REST (Лаб. 05 — для порівняння затримок).
/// null = ресурсу немає (404 / NOT_FOUND); виняток = сервіс недоступний або зламався.
/// </summary>
public interface IBookingCardSource
{
    string Transport { get; }
    Task<BookingResponse?> GetBookingAsync(long id, CancellationToken ct);
    Task<CustomerResponse?> GetCustomerAsync(long id, CancellationToken ct);
    Task<IReadOnlyList<ShowtimeResponse>> GetShowtimesAsync(IReadOnlyCollection<long> ids, CancellationToken ct);
    Task<MovieResponse?> GetMovieAsync(long id, CancellationToken ct);
    Task<MovieRatingResponse?> GetRatingAsync(long movieId, CancellationToken ct);
    Task<ReviewsPageResponse?> GetLatestReviewsAsync(long movieId, int take, CancellationToken ct);
}

/// <summary>HTTP REST через typed HttpClients з Лаб. 05.</summary>
public sealed class HttpBookingCardSource(BookingsClient bookings, CatalogClient catalog, ReviewsClient reviews) : IBookingCardSource
{
    public string Transport => "http";
    public Task<BookingResponse?> GetBookingAsync(long id, CancellationToken ct) => bookings.GetBookingAsync(id, ct);
    public Task<CustomerResponse?> GetCustomerAsync(long id, CancellationToken ct) => bookings.GetCustomerAsync(id, ct);

    public async Task<IReadOnlyList<ShowtimeResponse>> GetShowtimesAsync(IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        // REST не має пакетного ендпоінта — N паралельних HTTP-запитів
        var results = await Task.WhenAll(ids.Select(id => catalog.GetShowtimeAsync(id, ct)));
        return results.Where(s => s is not null).Select(s => s!).ToList();
    }

    public Task<MovieResponse?> GetMovieAsync(long id, CancellationToken ct) => catalog.GetMovieAsync(id, ct);
    public Task<MovieRatingResponse?> GetRatingAsync(long movieId, CancellationToken ct) => reviews.GetRatingAsync(movieId, ct);
    public Task<ReviewsPageResponse?> GetLatestReviewsAsync(long movieId, int take, CancellationToken ct) => reviews.GetLatestReviewsAsync(movieId, take, ct);
}
