using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using BookingsGrpc = Bookings.Grpc;
using CatalogGrpc = Catalog.Grpc;
using ReviewsGrpc = Reviews.Grpc;

namespace Platform.Tests;

/// <summary>
/// Aggregator на gRPC (Лаб. 06) проти фейкових gRPC-сервісів у TestServer (ті самі .proto, GrpcServices="Server").
/// Перевіряє паралельні виклики, часткові відмови через RpcException, кеш агрегованої відповіді й CorrelationId у метаданих.
/// </summary>
public sealed class GrpcAggregatorTests : IAsyncLifetime
{
    private sealed class State
    {
        public readonly ConcurrentDictionary<string, bool> Down = new();
        public readonly ConcurrentBag<(string Method, string? CorrelationId)> Calls = [];
        public int ShowtimeBatchCalls;

        public void Check(string service, ServerCallContext ctx)
        {
            Calls.Add((ctx.Method, ctx.RequestHeaders.GetValue("x-correlation-id")));
            if (Down.ContainsKey(service)) throw new RpcException(new Status(StatusCode.Unavailable, $"{service} is down"));
        }
    }

    private sealed class FakeBookings(State state) : BookingsGrpc.BookingService.BookingServiceBase
    {
        public override Task<BookingsGrpc.BookingReply> GetBooking(BookingsGrpc.GetBookingRequest request, ServerCallContext context)
        {
            state.Check("bookings", context);
            if (request.Id != 1) throw new RpcException(new Status(StatusCode.NotFound, "Booking not found"));
            var reply = new BookingsGrpc.BookingReply
            {
                Id = 1, CustomerId = 7, BookingCode = "BA000001", Status = "Paid", Channel = "Web",
                TotalAmount = new BookingsGrpc.DecimalValue { Units = 470 },
                BookedAt = Timestamp.FromDateTime(new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc))
            };
            foreach (var (seat, type, price) in new[] { (7, "Adult", 250L), (8, "Student", 220L) })
                reply.Tickets.Add(new BookingsGrpc.TicketReply
                {
                    ShowtimeId = 4, MovieTitle = "Дюна", SeatRow = 5, SeatNumber = seat, TicketType = type,
                    StartsAt = Timestamp.FromDateTime(new DateTime(2027, 1, 16, 19, 30, 0, DateTimeKind.Utc)),
                    UnitPrice = new BookingsGrpc.DecimalValue { Units = price }
                });
            return Task.FromResult(reply);
        }

        public override Task<BookingsGrpc.CustomerReply> GetCustomer(BookingsGrpc.GetCustomerRequest request, ServerCallContext context)
        {
            state.Check("bookings", context);
            return Task.FromResult(new BookingsGrpc.CustomerReply { Id = request.Id, FullName = "Олена Коваль" });
        }
    }

    private sealed class FakeCatalog(State state) : CatalogGrpc.CatalogService.CatalogServiceBase
    {
        public override Task<CatalogGrpc.ShowtimesReply> GetShowtimesByIds(CatalogGrpc.GetShowtimesByIdsRequest request, ServerCallContext context)
        {
            state.Check("catalog", context);
            Interlocked.Increment(ref state.ShowtimeBatchCalls);
            var reply = new CatalogGrpc.ShowtimesReply();
            if (request.Ids.Contains(4))
                reply.Showtimes.Add(new CatalogGrpc.ShowtimeReply
                {
                    Id = 4, MovieId = 3, MovieTitle = "Дюна: Частина друга", HallId = 1, HallName = "Зал 1 (IMAX)",
                    StartsAt = Timestamp.FromDateTime(new DateTime(2027, 1, 16, 19, 30, 0, DateTimeKind.Utc)),
                    Price = new CatalogGrpc.DecimalValue { Units = 250 }, Language = "uk"
                });
            return Task.FromResult(reply);
        }

        public override Task<CatalogGrpc.MovieReply> GetMovie(CatalogGrpc.GetMovieRequest request, ServerCallContext context)
        {
            state.Check("catalog", context);
            var reply = new CatalogGrpc.MovieReply { Id = 3, Title = "Дюна: Частина друга", ReleaseYear = 2024, DurationMin = 166, AgeRating = "12+", Director = "Дені Вільнев" };
            reply.Genres.Add(new CatalogGrpc.GenreReply { Id = 4, Name = "Фантастика", IsPrimary = true });
            return Task.FromResult(reply);
        }
    }

    private sealed class FakeReviews(State state) : ReviewsGrpc.ReviewsService.ReviewsServiceBase
    {
        public override Task<ReviewsGrpc.MovieRatingReply> GetMovieRating(ReviewsGrpc.GetMovieRatingRequest request, ServerCallContext context)
        {
            state.Check("reviews", context);
            return Task.FromResult(new ReviewsGrpc.MovieRatingReply { MovieId = request.MovieId, Average = 4.5, Count = 2 });
        }

        public override Task<ReviewsGrpc.ReviewsReply> GetLatestReviews(ReviewsGrpc.GetLatestReviewsRequest request, ServerCallContext context)
        {
            state.Check("reviews", context);
            var reply = new ReviewsGrpc.ReviewsReply { TotalCount = 1 };
            reply.Reviews.Add(new ReviewsGrpc.ReviewReply { Id = "6750c0000000000000000003", Type = "critic", AuthorName = "Андрій", Rating = 5, Text = "Звук!",
                CreatedAt = Timestamp.FromDateTime(new DateTime(2026, 11, 22, 21, 0, 0, DateTimeKind.Utc)) });
            return Task.FromResult(reply);
        }
    }

    private readonly State _state = new();
    private WebApplication _grpcHost = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddGrpc();
        builder.Services.AddSingleton(_state);
        _grpcHost = builder.Build();
        _grpcHost.MapGrpcService<FakeBookings>();
        _grpcHost.MapGrpcService<FakeCatalog>();
        _grpcHost.MapGrpcService<FakeReviews>();
        await _grpcHost.StartAsync();
        var grpcHandler = _grpcHost.GetTestServer().CreateHandler();

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("services:bookings-api:grpc:0", "http://bookings.test");
            b.UseSetting("services:catalog-api:grpc:0", "http://catalog.test");
            b.UseSetting("services:reviews-api:grpc:0", "http://reviews.test");
            b.ConfigureTestServices(s => s.ConfigureHttpClientDefaults(h => h.ConfigurePrimaryHttpMessageHandler(() => grpcHandler)));
        });
        _client = factory.CreateClient();
    }

    public async Task DisposeAsync() => await _grpcHost.DisposeAsync();

    private async Task<(JsonElement Json, HttpResponseMessage Response)> GetCardAsync(long id = 1, string? correlationId = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/aggregator/bookings/{id}/full");
        if (correlationId is not null) request.Headers.Add("X-Correlation-Id", correlationId);
        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return (body.Length > 0 ? JsonDocument.Parse(body).RootElement : default, response);
    }

    [Fact]
    public async Task Grpc_card_combines_three_services_with_one_batch_call_for_showtimes()
    {
        var (json, response) = await GetCardAsync(correlationId: "grpc-test-1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("grpc", response.Headers.GetValues("X-Aggregator-Transport").Single());
        var session = json.GetProperty("sessions")[0];
        Assert.Equal("Зал 1 (IMAX)", session.GetProperty("hallName").GetString());
        Assert.Equal(470m, json.GetProperty("booking").GetProperty("totalAmount").GetDecimal());   // DecimalValue → decimal
        Assert.Equal(4.5, session.GetProperty("audience").GetProperty("averageRating").GetDouble());
        Assert.Equal(1, _state.ShowtimeBatchCalls);
        Assert.All(_state.Calls, c => Assert.Equal("grpc-test-1", c.CorrelationId));            // CorrelationId у метаданих gRPC
    }

    [Fact]
    public async Task Unavailable_reviews_gives_warning_not_500_and_partial_is_not_cached()
    {
        _state.Down["reviews"] = true;
        var (json, response) = await GetCardAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(json.GetProperty("isPartial").GetBoolean());
        Assert.Contains(json.GetProperty("warnings").EnumerateArray(), w => w.GetString()!.StartsWith("reviews-api is unavailable"));

        _state.Down.TryRemove("reviews", out _);
        var (again, second) = await GetCardAsync();
        Assert.Equal("MISS", second.Headers.GetValues("X-Aggregator-Cache").Single());              // часткову не закешовано
        Assert.False(again.GetProperty("isPartial").GetBoolean());
    }

    [Fact]
    public async Task Full_card_is_cached_and_second_request_skips_grpc_calls()
    {
        var (_, first) = await GetCardAsync();
        var callsAfterFirst = _state.Calls.Count;
        var (json, second) = await GetCardAsync();

        Assert.Equal("MISS", first.Headers.GetValues("X-Aggregator-Cache").Single());
        Assert.Equal("HIT", second.Headers.GetValues("X-Aggregator-Cache").Single());
        Assert.Equal(callsAfterFirst, _state.Calls.Count);                                         // жодного gRPC-виклику
        Assert.Equal("BA000001", json.GetProperty("booking").GetProperty("code").GetString());
    }

    [Fact]
    public async Task NotFound_status_maps_to_404_and_unavailable_bookings_to_503()
    {
        var (_, missing) = await GetCardAsync(id: 42);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        _state.Down["bookings"] = true;
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/aggregator/bookings/1/full");
        request.Headers.Add("Cache-Control", "no-cache");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await _client.SendAsync(request)).StatusCode);
    }
}
