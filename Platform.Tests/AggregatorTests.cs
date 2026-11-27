using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.Tests;

/// <summary>Aggregator, транспорт HTTP (Лаб. 05), на заглушках REST-сервісів (service discovery через конфігурацію).</summary>
public sealed class AggregatorTests
{
    private sealed class StubServices : HttpMessageHandler
    {
        public readonly HashSet<string> Down = [];
        public readonly ConcurrentBag<(string Host, string? CorrelationId)> Calls = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var host = request.RequestUri!.Host;
            var path = request.RequestUri.PathAndQuery;
            Calls.Add((host, request.Headers.TryGetValues("X-Correlation-Id", out var v) ? v.First() : null));
            if (Down.Contains(host)) throw new HttpRequestException($"Connection refused ({host})");

            object? body = (host, path) switch
            {
                ("bookings.test", "/api/bookings/1") => new
                {
                    id = 1, customerId = 7, bookingCode = "BA000001", status = "Paid", totalAmount = 470m,
                    bookedAt = "2026-11-20T10:00:00", channel = "Web",
                    tickets = new object[]
                    {
                        new { showtimeId = 4, movieTitle = "Дюна", startsAt = "2027-01-16T19:30:00", seatRow = 5, seatNumber = 7, ticketType = "Adult", unitPrice = 250m },
                        new { showtimeId = 4, movieTitle = "Дюна", startsAt = "2027-01-16T19:30:00", seatRow = 5, seatNumber = 8, ticketType = "Student", unitPrice = 220m }
                    }
                },
                ("bookings.test", "/api/customers/7") => new { id = 7, fullName = "Олена Коваль", email = "secret@example.com" },
                ("catalog.test", "/api/showtimes/4") => new
                {
                    id = 4, movieId = 3, movieTitle = "Дюна: Частина друга", hallId = 1, hallName = "Зал 1 (IMAX)",
                    startsAt = "2027-01-16T19:30:00", price = 250m, language = "uk", isCancelled = false
                },
                ("catalog.test", "/api/movies/3") => new
                {
                    id = 3, title = "Дюна: Частина друга", releaseYear = 2024, durationMin = 166, ageRating = "12+",
                    detail = new { director = "Дені Вільнев", country = "США" },
                    genres = new object[] { new { name = "Фантастика", isPrimary = true } }
                },
                ("reviews.test", "/api/movies/3/rating") => new { movieId = 3, average = 4.0, count = 2 },
                ("reviews.test", var p) when p.StartsWith("/api/reviews?movieId=3") => new
                {
                    items = new object[] { new { id = "6750c0000000000000000003", type = "critic", author = new { displayName = "Андрій" }, rating = 5, text = "Звук!", createdAt = "2026-11-22T21:00:00Z" } },
                    totalCount = 1
                },
                _ => null
            };

            var response = body is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
                };
            return Task.FromResult(response);
        }
    }

    private static (HttpClient Client, StubServices Stub) Create()
    {
        var stub = new StubServices();
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("services:bookings-api:http:0", "http://bookings.test");
            b.UseSetting("services:catalog-api:http:0", "http://catalog.test");
            b.UseSetting("services:reviews-api:http:0", "http://reviews.test");
            b.ConfigureTestServices(s => s.ConfigureHttpClientDefaults(h => h.ConfigurePrimaryHttpMessageHandler(() => stub)));
        });
        return (factory.CreateClient(), stub);
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string url, HttpStatusCode expected)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(expected, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    [Fact]
    public async Task Full_card_combines_three_services()
    {
        var (client, stub) = Create();
        var json = await GetJsonAsync(client, "/api/aggregator/bookings/1/full?transport=http", HttpStatusCode.OK);

        Assert.Equal("BA000001", json.GetProperty("booking").GetProperty("code").GetString());
        Assert.Equal("Олена Коваль", json.GetProperty("customer").GetProperty("fullName").GetString());
        Assert.False(json.GetProperty("customer").TryGetProperty("email", out _));          // зайве не потрапляє
        var session = json.GetProperty("sessions")[0];
        Assert.Equal("Зал 1 (IMAX)", session.GetProperty("hallName").GetString());
        Assert.Equal(2, session.GetProperty("seats").GetArrayLength());
        Assert.Equal("Фантастика", session.GetProperty("movie").GetProperty("primaryGenre").GetString());
        Assert.Equal(4.0, session.GetProperty("audience").GetProperty("averageRating").GetDouble());
        Assert.Equal(0, json.GetProperty("warnings").GetArrayLength());
        Assert.Contains(stub.Calls, c => c.Host == "reviews.test");
    }

    [Fact]
    public async Task Reviews_down_gives_partial_card_with_warning_not_500()
    {
        var (client, stub) = Create();
        stub.Down.Add("reviews.test");

        var json = await GetJsonAsync(client, "/api/aggregator/bookings/1/full?transport=http", HttpStatusCode.OK);

        var session = json.GetProperty("sessions")[0];
        Assert.True(session.TryGetProperty("movie", out _));                               // каталог є
        Assert.False(session.TryGetProperty("audience", out _));                           // null не серіалізується
        Assert.True(json.GetProperty("isPartial").GetBoolean());
        Assert.Contains(json.GetProperty("warnings").EnumerateArray(), w => w.GetString()!.StartsWith("reviews-api is unavailable"));
    }

    [Fact]
    public async Task Catalog_down_keeps_ticket_snapshot_data()
    {
        var (client, stub) = Create();
        stub.Down.Add("catalog.test");

        var json = await GetJsonAsync(client, "/api/aggregator/bookings/1/full?transport=http", HttpStatusCode.OK);

        var session = json.GetProperty("sessions")[0];
        Assert.Equal("Дюна", session.GetProperty("movieTitle").GetString());              // знімок із квитка
        Assert.False(session.TryGetProperty("hallName", out _));
        Assert.Contains(json.GetProperty("warnings").EnumerateArray(), w => w.GetString()!.StartsWith("catalog-api is unavailable"));
    }

    [Fact]
    public async Task Bookings_down_is_503_and_unknown_booking_is_404()
    {
        var (client, stub) = Create();
        await GetJsonAsync(client, "/api/aggregator/bookings/999/full?transport=http", HttpStatusCode.NotFound);
        stub.Down.Add("bookings.test");
        await GetJsonAsync(client, "/api/aggregator/bookings/1/full?transport=http", HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task CorrelationId_is_returned_and_forwarded_to_every_service()
    {
        var (client, stub) = Create();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/aggregator/bookings/1/full?transport=http");
        request.Headers.Add("X-Correlation-Id", "test-correlation-42");

        var response = await client.SendAsync(request);

        Assert.Equal("test-correlation-42", response.Headers.GetValues("X-Correlation-Id").Single());
        Assert.NotEmpty(stub.Calls);
        Assert.All(stub.Calls, c => Assert.Equal("test-correlation-42", c.CorrelationId));
    }
}
