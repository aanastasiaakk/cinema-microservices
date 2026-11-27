using Grpc.Core;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Services;
using BookingsGrpc = Bookings.Grpc;
using CatalogGrpc = Catalog.Grpc;
using ReviewsGrpc = Reviews.Grpc;

namespace Platform.Aggregator.Grpc;

/// <summary>
/// gRPC-транспорт: типізовані клієнти, згенеровані з .proto сервісів (GrpcServices="Client"), зареєстровані
/// через AddGrpcClient з адресою за логічним іменем (http://_grpc.catalog-api → endpoint "grpc" ресурсу
/// catalog-api через service discovery). Канал HTTP/2 мультиплексує паралельні виклики в одне з'єднання.
/// Кожен виклик має deadline і CancellationToken; NOT_FOUND → null; інші статуси — RpcException назовні.
/// X-Correlation-Id потрапляє в метадані автоматично (CorrelationIdHandler із ServiceDefaults).
/// </summary>
public sealed class GrpcBookingCardSource(
    BookingsGrpc.BookingService.BookingServiceClient bookings,
    CatalogGrpc.CatalogService.CatalogServiceClient catalog,
    ReviewsGrpc.ReviewsService.ReviewsServiceClient reviews) : IBookingCardSource
{
    private static readonly TimeSpan CallDeadline = TimeSpan.FromSeconds(3);
    private static DateTime Deadline => DateTime.UtcNow.Add(CallDeadline);

    public string Transport => "grpc";

    public Task<BookingResponse?> GetBookingAsync(long id, CancellationToken ct) => OrNull(async () =>
    {
        var b = await bookings.GetBookingAsync(new BookingsGrpc.GetBookingRequest { Id = id }, deadline: Deadline, cancellationToken: ct);
        return new BookingResponse(b.Id, b.CustomerId, b.BookingCode, b.Status, ToDecimal(b.TotalAmount), Local(b.BookedAt),
            b.Channel, b.Tickets.Select(t => new BookingTicket(t.ShowtimeId, t.MovieTitle, Local(t.StartsAt), t.SeatRow,
                t.SeatNumber, t.TicketType, ToDecimal(t.UnitPrice))).ToList());
    });

    public Task<CustomerResponse?> GetCustomerAsync(long id, CancellationToken ct) => OrNull(async () =>
    {
        var c = await bookings.GetCustomerAsync(new BookingsGrpc.GetCustomerRequest { Id = id }, deadline: Deadline, cancellationToken: ct);
        return new CustomerResponse(c.Id, c.FullName);
    });

    public async Task<IReadOnlyList<ShowtimeResponse>> GetShowtimesAsync(IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        // ОДИН пакетний виклик замість N запитів REST
        var request = new CatalogGrpc.GetShowtimesByIdsRequest();
        request.Ids.AddRange(ids);
        var reply = await catalog.GetShowtimesByIdsAsync(request, deadline: Deadline, cancellationToken: ct);
        return reply.Showtimes.Select(s => new ShowtimeResponse(s.Id, s.MovieId, s.MovieTitle, s.HallId, s.HallName,
            Local(s.StartsAt), ToDecimal(s.Price), s.Language, s.IsCancelled)).ToList();
    }

    public Task<MovieResponse?> GetMovieAsync(long id, CancellationToken ct) => OrNull(async () =>
    {
        var m = await catalog.GetMovieAsync(new CatalogGrpc.GetMovieRequest { Id = id }, deadline: Deadline, cancellationToken: ct);
        return new MovieResponse(m.Id, m.Title, (short)m.ReleaseYear, (short)m.DurationMin, m.AgeRating,
            new MovieDetailResponse(Empty(m.Director), Empty(m.Country)),
            m.Genres.Select(g => new MovieGenreResponse(g.Name, g.IsPrimary)).ToList());
    });

    public Task<MovieRatingResponse?> GetRatingAsync(long movieId, CancellationToken ct) => OrNull(async () =>
    {
        var r = await reviews.GetMovieRatingAsync(new ReviewsGrpc.GetMovieRatingRequest { MovieId = movieId }, deadline: Deadline, cancellationToken: ct);
        return new MovieRatingResponse(r.MovieId, r.Average, r.Count);
    });

    public Task<ReviewsPageResponse?> GetLatestReviewsAsync(long movieId, int take, CancellationToken ct) => OrNull(async () =>
    {
        var r = await reviews.GetLatestReviewsAsync(new ReviewsGrpc.GetLatestReviewsRequest { MovieId = movieId, Take = take },
            deadline: Deadline, cancellationToken: ct);
        return new ReviewsPageResponse(r.Reviews.Select(x => new ReviewItemResponse(x.Id, x.Type,
            new ReviewAuthorResponse(x.AuthorName), x.Rating, Empty(x.Text), x.CreatedAt.ToDateTime())).ToList(), r.TotalCount);
    });

    private static async Task<T?> OrNull<T>(Func<Task<T>> call) where T : class
    {
        try
        {
            return await call();
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
    }

    private static decimal ToDecimal(BookingsGrpc.DecimalValue v) => v.Units + v.Nanos / 1_000_000_000m;
    private static decimal ToDecimal(CatalogGrpc.DecimalValue v) => v.Units + v.Nanos / 1_000_000_000m;
    private static DateTime Local(Google.Protobuf.WellKnownTypes.Timestamp t) => DateTime.SpecifyKind(t.ToDateTime(), DateTimeKind.Unspecified);
    private static string? Empty(string value) => string.IsNullOrEmpty(value) ? null : value;
}
