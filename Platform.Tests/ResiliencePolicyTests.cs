using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Platform.Aggregator.Resilience;

namespace Platform.Tests;

/// <summary>Політики resilience Aggregator: різні для критичних і некритичних сервісів, circuit breaker розмикається.</summary>
public sealed class ResiliencePolicyTests
{
    [Fact]
    public void Critical_catalog_retries_more_and_breaks_sooner_than_non_critical_reviews()
    {
        Assert.True(ServicePolicy.Catalog.HttpRetries > ServicePolicy.Reviews.HttpRetries);
        Assert.True(ServicePolicy.Catalog.GrpcMaxAttempts > ServicePolicy.Reviews.GrpcMaxAttempts);
        Assert.True(ServicePolicy.Catalog.FailureRatio < ServicePolicy.Reviews.FailureRatio);          // чутливіший breaker
        Assert.True(ServicePolicy.Reviews.TotalTimeout < ServicePolicy.Catalog.TotalTimeout);          // некритичний швидко здається
        foreach (var p in new[] { ServicePolicy.Catalog, ServicePolicy.Bookings, ServicePolicy.Reviews })
        {
            Assert.True(p.SamplingDuration >= 2 * p.AttemptTimeout, p.Service);
            Assert.True(p.TotalTimeout >= p.AttemptTimeout * (p.HttpRetries + 1), p.Service);          // повтори встигають
        }
    }

    [Fact]
    public void Each_client_gets_its_own_pipeline_options_instead_of_a_second_handler()
    {
        using var factory = new WebApplicationFactory<Program>();
        var options = factory.Services.GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>();

        var reviews = options.Get("ReviewsClient-standard");
        var catalog = options.Get("CatalogClient-standard");

        Assert.Equal(ServicePolicy.Reviews.HttpRetries, reviews.Retry.MaxRetryAttempts);
        Assert.Equal(ServicePolicy.Catalog.HttpRetries, catalog.Retry.MaxRetryAttempts);
        Assert.Equal(ServicePolicy.Reviews.AttemptTimeout, reviews.AttemptTimeout.Timeout);
        Assert.True(reviews.Retry.UseJitter);
        Assert.Equal(ServicePolicy.Catalog.FailureRatio, catalog.CircuitBreaker.FailureRatio);
    }

    private sealed class Stub : HttpMessageHandler
    {
        public readonly ConcurrentDictionary<string, int> Calls = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var host = request.RequestUri!.Host;
            Calls.AddOrUpdate(host, 1, (_, n) => n + 1);
            if (host == "reviews.test") throw new HttpRequestException("Connection refused (reviews.test)");
            object? body = (host, request.RequestUri.AbsolutePath) switch
            {
                ("bookings.test", "/api/bookings/1") => new
                {
                    id = 1, customerId = 7, bookingCode = "BA000001", status = "Paid", totalAmount = 250m, bookedAt = "2026-11-20T10:00:00", channel = "Web",
                    tickets = new[] { new { showtimeId = 4, movieTitle = "Дюна", startsAt = "2027-01-16T19:30:00", seatRow = 5, seatNumber = 7, ticketType = "Adult", unitPrice = 250m } }
                },
                ("bookings.test", "/api/customers/7") => new { id = 7, fullName = "Олена" },
                ("catalog.test", "/api/showtimes/4") => new { id = 4, movieId = 3, movieTitle = "Дюна", hallId = 1, hallName = "Зал 1", startsAt = "2027-01-16T19:30:00", price = 250m, language = "uk", isCancelled = false },
                ("catalog.test", "/api/movies/3") => new { id = 3, title = "Дюна", releaseYear = 2024, durationMin = 166, ageRating = "12+", genres = Array.Empty<object>() },
                _ => null
            };
            return Task.FromResult(body is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") });
        }
    }

    [Fact]
    public async Task Circuit_breaker_opens_for_failing_reviews_and_rejects_calls_without_reaching_it()
    {
        var stub = new Stub();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("services:bookings-api:http:0", "http://bookings.test");
            b.UseSetting("services:catalog-api:http:0", "http://catalog.test");
            b.UseSetting("services:reviews-api:http:0", "http://reviews.test");
            b.ConfigureTestServices(s => s.ConfigureHttpClientDefaults(h => h.ConfigurePrimaryHttpMessageHandler(() => stub)));
        });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");

        for (var i = 0; i < 4; i++)                                    // ≥ MinimumThroughput невдалих спроб → Open
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/aggregator/bookings/1/full?transport=http")).StatusCode);
        var callsWhenOpen = stub.Calls["reviews.test"];

        var response = await client.GetAsync("/api/aggregator/bookings/1/full?transport=http");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);                                       // часткова, не 500
        Assert.Equal(callsWhenOpen, stub.Calls["reviews.test"]);                                    // fail-fast: до сервісу не дійшло
        Assert.True(json.GetProperty("isPartial").GetBoolean());
        Assert.True(stub.Calls["catalog.test"] > 0);
    }
}
