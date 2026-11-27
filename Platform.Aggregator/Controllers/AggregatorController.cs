using Microsoft.AspNetCore.Mvc;
using Platform.Aggregator.Caching;
using Platform.Aggregator.Grpc;
using Platform.Aggregator.Models;
using Platform.Aggregator.Services;

namespace Platform.Aggregator.Controllers;

/// <summary>Агрегувальні ендпоінти (через Gateway: /api/aggregator/...).</summary>
[ApiController]
[Route("api/aggregator")]
[Produces("application/json")]
public sealed class AggregatorController(BookingCardAggregator aggregator, GrpcBookingCardSource grpc,
                                         HttpBookingCardSource http, AggregatedResponseCache cache) : ControllerBase
{
    /// <summary>
    /// Повна картка бронювання: бронювання й клієнт (bookings-api) + сеанси й фільми (catalog-api) +
    /// рейтинг і останні відгуки (reviews-api). Якщо catalog-api чи reviews-api недоступні — 200 з тим, що є,
    /// і переліком у warnings; bookings-api недоступний — 503.
    /// transport: grpc (за замовчуванням, Лаб. 06) | http (Лаб. 05, для порівняння затримок).
    /// Відповідь кешується в Redis на 30 с (заголовок X-Aggregator-Cache: HIT | MISS | BYPASS);
    /// Cache-Control: no-cache — оминути кеш (вимірювання).
    /// </summary>
    [HttpGet("bookings/{id:long}/full")]
    [ProducesResponseType<FullBookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<FullBookingDto>> GetFullBooking(long id, [FromQuery] string transport = "grpc",
                                                                   CancellationToken ct = default)
    {
        IBookingCardSource? source = transport.ToLowerInvariant() switch { "grpc" => grpc, "http" => http, _ => null };
        if (source is null)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Unknown transport", detail: "transport must be 'grpc' or 'http'");
        Response.Headers["X-Aggregator-Transport"] = source.Transport;

        var bypass = Request.Headers.CacheControl.ToString().Contains("no-cache", StringComparison.OrdinalIgnoreCase);
        var key = AggregatedResponseCache.Key(id, source.Transport);
        if (!bypass && await cache.TryGetAsync(key, ct) is { } cached)              // кеш перевіряється ДО викликів сервісів
        {
            Response.Headers["X-Aggregator-Cache"] = "HIT";
            return Ok(cached);
        }
        Response.Headers["X-Aggregator-Cache"] = bypass ? "BYPASS" : "MISS";

        try
        {
            var card = await aggregator.GetFullBookingAsync(source, id, ct);
            if (card is null)
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "Booking not found", detail: $"Booking {id} was not found");
            await cache.SetAsync(key, card, ct);                                   // часткові не кешуються
            return Ok(card);
        }
        catch (UpstreamUnavailableException ex)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Required service unavailable",
                           detail: $"{ex.Service} is unavailable, the booking card cannot be built");
        }
    }
}
