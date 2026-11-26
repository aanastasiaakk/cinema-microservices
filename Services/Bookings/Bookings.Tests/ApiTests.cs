using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bookings.Tests;

/// <summary>API-тести через WebApplicationFactory проти справжньої БД (BOOKINGS_TEST_DB).</summary>
public class ApiTests
{
    private static HttpClient CreateClient() =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b =>
            {
                b.UseEnvironment("Development");
                b.UseSetting("ConnectionStrings:CinemaBooking", TestDb.ConnectionString);
            })
            .CreateClient();

    [DbFact]
    public async Task Unknown_booking_returns_404_ProblemDetails_not_500()
    {
        var response = await CreateClient().GetAsync("/api/bookings/999999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(404, problem!.Status);
    }

    [DbFact]
    public async Task Invalid_status_transition_returns_409_ProblemDetails()
    {
        var client = CreateClient();
        // SEED0001 оплачене -> скасувати не можна
        var bookingId = await FindSeedBookingAsync(client, "olena.koval@example.com", "SEED0001");

        var response = await client.PostAsJsonAsync($"/api/bookings/{bookingId}/cancel", new { reason = "test" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("Invalid status transition", problem!.Detail);
    }

    [DbFact]
    public async Task Invalid_dto_returns_400_before_service_call()
    {
        var response = await CreateClient().PostAsJsonAsync("/api/customers", new { email = "not-an-email", fullName = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<long> FindSeedBookingAsync(HttpClient client, string email, string code)
    {
        var customers = await client.GetFromJsonAsync<List<Bll.Dto.CustomerDto>>("/api/customers?pageSize=100");
        var customer = customers!.Single(c => c.Email == email);
        var bookings = await client.GetFromJsonAsync<List<Bll.Dto.BookingSummaryDto>>($"/api/customers/{customer.Id}/bookings");
        return bookings!.Single(b => b.BookingCode == code).Id;
    }
}
