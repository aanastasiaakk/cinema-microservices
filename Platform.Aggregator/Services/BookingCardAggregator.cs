using System.Collections.Concurrent;
using System.Diagnostics;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Services;

/// <summary>Сервіс-джерело (bookings-api) недоступний — без нього картку не зібрати → 503.</summary>
public sealed class UpstreamUnavailableException(string service, Exception inner)
    : Exception($"{service} is unavailable", inner)
{
    public string Service { get; } = service;
}

/// <summary>
/// Aggregator Pattern: картка бронювання з трьох сервісів.
///  1) bookings-api: бронювання (обов'язкове; 404 → null, недоступний → 503);
///  2) ПАРАЛЕЛЬНО (Task.WhenAll): клієнт (bookings-api) і всі сеанси бронювання (catalog-api);
///  3) ПАРАЛЕЛЬНО (Task.WhenAll) для кожного фільму: фільм (catalog-api), рейтинг і останні відгуки (reviews-api).
/// Недоступність catalog-api чи reviews-api не валить запит: повертаються доступні дані + Warnings.
/// CancellationToken передається в усі виклики; кожен виклик логується з тривалістю.
/// </summary>
public sealed class BookingCardAggregator(BookingsClient bookings, CatalogClient catalog, ReviewsClient reviews,
                                          ILogger<BookingCardAggregator> logger)
{
    public static readonly ActivitySource Source = new("Cinema.Aggregator");
    private const int LatestReviews = 3;

    public async Task<FullBookingDto?> GetFullBookingAsync(long bookingId, CancellationToken ct)
    {
        using var activity = Source.StartActivity("AggregateBookingCard");
        activity?.SetTag("booking.id", bookingId);
        var warnings = new ConcurrentQueue<string>();

        // 1) кореневі дані
        BookingResponse? booking;
        try
        {
            booking = await TimedAsync(BookingsClient.ServiceName, $"booking {bookingId}",
                () => bookings.GetBookingAsync(bookingId, ct));
        }
        catch (Exception ex) when (IsUpstreamFailure(ex, ct))
        {
            throw new UpstreamUnavailableException(BookingsClient.ServiceName, ex);
        }
        if (booking is null) return null;

        // 2) паралельно: клієнт + сеанси
        var showtimeIds = booking.Tickets.Select(t => t.ShowtimeId).Distinct().ToList();
        var customerTask = SafeAsync(BookingsClient.ServiceName, $"customer {booking.CustomerId}",
            () => bookings.GetCustomerAsync(booking.CustomerId, ct), warnings, ct);
        var showtimesTask = Task.WhenAll(showtimeIds.Select(id => SafeAsync(CatalogClient.ServiceName, $"showtime {id}",
            () => catalog.GetShowtimeAsync(id, ct), warnings, ct)));
        await Task.WhenAll(customerTask, showtimesTask);
        var customer = await customerTask;
        var showtimes = (await showtimesTask)
            .Where(s => s is not null && showtimeIds.Contains(s.Id))           // узгодженість: лише сеанси цього бронювання
            .ToDictionary(s => s!.Id, s => s!);

        // 3) паралельно для кожного фільму (і всередині — паралельно каталог + рейтинг + відгуки)
        var movieIds = showtimes.Values.Select(s => s.MovieId).Distinct().ToList();
        if (movieIds.Count == 0 && showtimeIds.Count > 0)
            warnings.Enqueue("Movie details and reviews are unavailable because showtimes could not be loaded from catalog-api");
        var movieData = (await Task.WhenAll(movieIds.Select(id => LoadMovieAsync(id, warnings, ct))))
            .ToDictionary(m => m.MovieId);

        var reviewsDown = warnings.Any(w => w.StartsWith(ReviewsClient.ServiceName, StringComparison.Ordinal));

        // композиція: структура під сценарій, а не копія відповідей сервісів
        var sessions = booking.Tickets
            .GroupBy(t => t.ShowtimeId)
            .Select(g =>
            {
                showtimes.TryGetValue(g.Key, out var st);
                var first = g.First();
                MovieInfo? movie = null;
                AudienceOpinion? audience = null;
                if (st is not null && movieData.TryGetValue(st.MovieId, out var data))
                {
                    if (data.Movie is { } m)
                        movie = new MovieInfo(m.Id, m.Title, m.ReleaseYear, m.DurationMin, m.AgeRating,
                            m.Genres.FirstOrDefault(x => x.IsPrimary)?.Name, m.Detail?.Director);
                    if (!reviewsDown)
                        audience = new AudienceOpinion(data.Rating?.Average, data.Rating?.Count ?? 0,
                            (data.Latest?.Items ?? []).Select(r => new ReviewSnippet(r.Id, r.Author.DisplayName, r.Rating, r.Text, r.CreatedAt)).ToList());
                }
                return new SessionCard(g.Key, st?.StartsAt ?? first.StartsAt, st?.MovieTitle ?? first.MovieTitle,
                    st?.HallName, st?.Language, st?.IsCancelled,
                    g.Select(t => new SeatInfo(t.SeatRow, t.SeatNumber, t.TicketType, t.UnitPrice)).ToList(), movie, audience);
            })
            .OrderBy(s => s.StartsAt)
            .ToList();

        var result = new FullBookingDto(
            new BookingSummary(booking.Id, booking.BookingCode, booking.Status, booking.TotalAmount, booking.BookedAt,
                               booking.Channel, booking.Tickets.Count),
            customer is null ? null : new CustomerSummary(customer.Id, customer.FullName),
            sessions,
            warnings.Distinct().ToList());

        activity?.SetTag("aggregation.partial", result.IsPartial);
        logger.LogInformation("Booking card {BookingId} aggregated: {SessionsCount} sessions, {MoviesCount} movies, partial {IsPartial}, warnings {WarningsCount}",
            bookingId, sessions.Count, movieIds.Count, result.IsPartial, result.Warnings.Count);
        return result;
    }

    private sealed record MovieData(long MovieId, MovieResponse? Movie, MovieRatingResponse? Rating, ReviewsPageResponse? Latest);

    /// <summary>Фільм із каталогу і думка глядачів — три незалежні виклики паралельно.</summary>
    private async Task<MovieData> LoadMovieAsync(long movieId, ConcurrentQueue<string> warnings, CancellationToken ct)
    {
        var movieTask = SafeAsync(CatalogClient.ServiceName, $"movie {movieId}", () => catalog.GetMovieAsync(movieId, ct), warnings, ct);
        var ratingTask = SafeAsync(ReviewsClient.ServiceName, $"rating of movie {movieId}", () => reviews.GetRatingAsync(movieId, ct), warnings, ct);
        var latestTask = SafeAsync(ReviewsClient.ServiceName, $"latest reviews of movie {movieId}",
            () => reviews.GetLatestReviewsAsync(movieId, LatestReviews, ct), warnings, ct);
        await Task.WhenAll(movieTask, ratingTask, latestTask);
        return new MovieData(movieId, await movieTask, await ratingTask, await latestTask);
    }

    /// <summary>Виклик із вимірюванням часу: «Calling catalog-api for showtime 4» → «catalog-api responded in 85 ms».</summary>
    private async Task<T?> TimedAsync<T>(string service, string what, Func<Task<T?>> call)
    {
        var sw = Stopwatch.StartNew();
        logger.LogInformation("Calling {Service} for {Resource}", service, what);
        var result = await call();
        if (result is null)
            logger.LogInformation("{Service} responded for {Resource} in {ElapsedMs} ms: not found", service, what, sw.ElapsedMilliseconds);
        else
            logger.LogInformation("{Service} responded for {Resource} in {ElapsedMs} ms", service, what, sw.ElapsedMilliseconds);
        return result;
    }

    /// <summary>Часткова відмова: помилку логуємо, додаємо попередження і повертаємо default замість винятку.</summary>
    private async Task<T?> SafeAsync<T>(string service, string what, Func<Task<T?>> call,
                                        ConcurrentQueue<string> warnings, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return await TimedAsync(service, what, call);
        }
        catch (Exception ex) when (IsUpstreamFailure(ex, ct))
        {
            logger.LogWarning(ex, "{Service} is unavailable for {Resource} after {ElapsedMs} ms: {Error}",
                service, what, sw.ElapsedMilliseconds, ex.Message);
            warnings.Enqueue($"{service} is unavailable: {what} could not be loaded");
            return default;
        }
    }

    /// <summary>Будь-яка помилка виклику, КРІМ скасування самим клієнтом (тоді скасовуємо все).</summary>
    private static bool IsUpstreamFailure(Exception ex, CancellationToken ct)
        => !(ex is OperationCanceledException && ct.IsCancellationRequested);
}
