using System.ComponentModel.DataAnnotations;
using Bookings.Bll.Dto;
using Bookings.Bll.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bookings.Api.Controllers;

/// <summary>Розклад сеансів (локальна копія з Каталогу, лише читання).</summary>
[ApiController]
[Route("api/showtimes")]
[Produces("application/json")]
public sealed class ShowtimesController(IShowtimeService showtimes) : ControllerBase
{
    /// <summary>Сеанси від дати (за замовчуванням — від зараз) на N днів уперед.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ShowtimeDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ShowtimeDto>>> GetUpcoming(
        [FromQuery] DateTime? from,
        [FromQuery, Range(1, 365)] int days = 90,
        CancellationToken ct = default)
        => Ok(await showtimes.GetUpcomingAsync(from, days, ct));

    /// <summary>Сеанс за Id.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType<ShowtimeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShowtimeDto>> GetById(long id, CancellationToken ct)
        => Ok(await showtimes.GetAsync(id, ct));

    /// <summary>Зайняті місця сеансу.</summary>
    [HttpGet("{id:long}/seats")]
    [ProducesResponseType<ShowtimeSeatsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShowtimeSeatsDto>> GetSeats(long id, CancellationToken ct)
        => Ok(await showtimes.GetSeatsAsync(id, ct));
}
