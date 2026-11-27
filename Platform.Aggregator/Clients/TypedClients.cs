using System.Net;
using System.Net.Http.Json;

namespace Platform.Aggregator.Clients;

/// <summary>
/// Typed HttpClient-и: окремий клас на кожен сервіс. BaseAddress — логічне ім'я ресурсу Aspire
/// ("https+http://bookings-api" = HTTPS, якщо доступний, інакше HTTP), реальну адресу підставляє
/// service discovery. CorrelationId, стійкість і трасування додає ServiceDefaults до кожного клієнта.
/// 404 → null (такого ресурсу немає), інші помилки → виняток (сервіс недоступний або зламався).
/// </summary>
public abstract class ServiceClient(HttpClient http)
{
    protected async Task<T?> GetOrNullAsync<T>(string path, CancellationToken ct) where T : class
    {
        using var response = await http.GetAsync(path, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(ct);
    }
}

public sealed class BookingsClient(HttpClient http) : ServiceClient(http)
{
    public const string ServiceName = "bookings-api";

    public Task<BookingResponse?> GetBookingAsync(long id, CancellationToken ct)
        => GetOrNullAsync<BookingResponse>($"/api/bookings/{id}", ct);

    public Task<CustomerResponse?> GetCustomerAsync(long id, CancellationToken ct)
        => GetOrNullAsync<CustomerResponse>($"/api/customers/{id}", ct);
}

public sealed class CatalogClient(HttpClient http) : ServiceClient(http)
{
    public const string ServiceName = "catalog-api";

    public Task<ShowtimeResponse?> GetShowtimeAsync(long id, CancellationToken ct)
        => GetOrNullAsync<ShowtimeResponse>($"/api/showtimes/{id}", ct);

    public Task<MovieResponse?> GetMovieAsync(long id, CancellationToken ct)
        => GetOrNullAsync<MovieResponse>($"/api/movies/{id}", ct);
}

public sealed class ReviewsClient(HttpClient http) : ServiceClient(http)
{
    public const string ServiceName = "reviews-api";

    /// <summary>null — у фільму ще немає відгуків (404 від reviews-api).</summary>
    public Task<MovieRatingResponse?> GetRatingAsync(long movieId, CancellationToken ct)
        => GetOrNullAsync<MovieRatingResponse>($"/api/movies/{movieId}/rating", ct);

    public Task<ReviewsPageResponse?> GetLatestReviewsAsync(long movieId, int take, CancellationToken ct)
        => GetOrNullAsync<ReviewsPageResponse>($"/api/reviews?movieId={movieId}&sortBy=createdAt&sortDir=desc&pageSize={take}", ct);
}
