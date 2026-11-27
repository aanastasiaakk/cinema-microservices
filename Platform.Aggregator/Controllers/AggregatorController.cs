using Microsoft.AspNetCore.Mvc;
using Platform.Aggregator.Models;
using Platform.Aggregator.Services;

namespace Platform.Aggregator.Controllers;

/// <summary>Агрегувальні ендпоінти (через Gateway: /api/aggregator/...).</summary>
[ApiController]
[Route("api/aggregator")]
[Produces("application/json")]
public sealed class AggregatorController(BookingCardAggregator aggregator) : ControllerBase
{
    /// <summary>
    /// Повна картка бронювання: бронювання й клієнт (bookings-api) + сеанси й фільми (catalog-api) +
    /// рейтинг і останні відгуки (reviews-api). Якщо catalog-api чи reviews-api недоступні — 200 з тим, що є,
    /// і переліком у warnings; bookings-api недоступний — 503.
    /// </summary>
    [HttpGet("bookings/{id:long}/full")]
    [ProducesResponseType<FullBookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<FullBookingDto>> GetFullBooking(long id, CancellationToken ct)
    {
        try
        {
            var card = await aggregator.GetFullBookingAsync(id, ct);
            return card is null
                ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Booking not found", detail: $"Booking {id} was not found")
                : Ok(card);
        }
        catch (UpstreamUnavailableException ex)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Required service unavailable",
                           detail: $"{ex.Service} is unavailable, the booking card cannot be built");
        }
    }
}
