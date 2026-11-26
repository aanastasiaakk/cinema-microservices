using Catalog.Bll.Dto;
using Catalog.Bll.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

/// <summary>Розклад сеансів і зали.</summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public sealed class ShowtimesController(IShowtimeService showtimes) : ControllerBase
{
    /// <summary>Розклад з пагінацією та фільтрами (movieId, hallId, from, to, includeCancelled); sortBy: startsAt | price.</summary>
    [HttpGet("showtimes")]
    [ProducesResponseType<PagedResult<ShowtimeDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ShowtimeDto>>> GetList(
        [FromQuery] ShowtimeQuery query, [FromServices] IValidator<ShowtimeQuery> validator, CancellationToken ct)
    {
        if (await this.ValidateAsync(validator, query, ct) is { } invalid) return invalid;
        var page = await showtimes.GetListAsync(query, ct);
        Response.AddPaginationHeaders(page);
        return Ok(page);
    }

    [HttpGet("showtimes/{id:long}")]
    [ProducesResponseType<ShowtimeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShowtimeDto>> GetById(long id, CancellationToken ct)
        => Ok(await showtimes.GetByIdAsync(id, ct));

    /// <summary>Запланувати сеанс. Зал зайнятий у цей час → 409; неактивний фільм → 409.</summary>
    [HttpPost("showtimes")]
    [ProducesResponseType<ShowtimeDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ShowtimeDto>> Create(
        CreateShowtimeDto input, [FromServices] IValidator<CreateShowtimeDto> validator, CancellationToken ct)
    {
        if (await this.ValidateAsync(validator, input, ct) is { } invalid) return invalid;
        var created = await showtimes.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Скасувати сеанс.</summary>
    [HttpPost("showtimes/{id:long}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct)
    {
        await showtimes.CancelAsync(id, ct);
        return NoContent();
    }

    /// <summary>Зали (кешуються).</summary>
    [HttpGet("halls")]
    [ProducesResponseType<IReadOnlyList<HallDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<HallDto>>> GetHalls(CancellationToken ct)
        => Ok(await showtimes.GetHallsAsync(ct));
}
