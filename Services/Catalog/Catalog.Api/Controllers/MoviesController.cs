using Catalog.Bll.Dto;
using Catalog.Bll.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

/// <summary>Фільми. Тонкий контролер: валідація входу → сервіс BLL; DbContext не використовується.</summary>
[ApiController]
[Route("api/movies")]
[Produces("application/json")]
public sealed class MoviesController(IMovieService movies) : ControllerBase
{
    /// <summary>Афіша з пагінацією, фільтрами (search, genre, year, ageRating, minPrice, maxPrice, isActive) і сортуванням.</summary>
    /// <remarks>Приклад: <c>GET /api/movies?page=2&amp;pageSize=5&amp;sortBy=price&amp;sortDir=desc</c>. Метадані — у тілі та заголовках X-Total-Count / X-Total-Pages.</remarks>
    [HttpGet]
    [ProducesResponseType<PagedResult<MovieListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<MovieListItemDto>>> GetList(
        [FromQuery] MovieQuery query, [FromServices] IValidator<MovieQuery> validator, CancellationToken ct)
    {
        if (await this.ValidateAsync(validator, query, ct) is { } invalid) return invalid;
        var page = await movies.GetListAsync(query, ct);
        Response.AddPaginationHeaders(page);
        return Ok(page);
    }

    /// <summary>Картка фільму: деталі, жанри, сеанси (Eager Loading — один SQL-запит).</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType<MovieDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MovieDto>> GetById(long id, CancellationToken ct)
        => Ok(await movies.GetByIdAsync(id, ct));

    /// <summary>Створити фільм (Movies + MovieDetails + MovieGenres одним SaveChanges). Дубль назви+року → 409.</summary>
    [HttpPost]
    [ProducesResponseType<MovieDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MovieDto>> Create(
        CreateMovieDto input, [FromServices] IValidator<CreateMovieDto> validator, CancellationToken ct)
    {
        if (await this.ValidateAsync(validator, input, ct) is { } invalid) return invalid;
        var created = await movies.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Змінити базову ціну.</summary>
    [HttpPut("{id:long}/price")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangePrice(long id, ChangePriceDto input, CancellationToken ct)
    {
        await movies.ChangePriceAsync(id, input.Price, ct);
        return NoContent();
    }

    /// <summary>Зняти з афіші (409, якщо є майбутні сеанси).</summary>
    [HttpPost("{id:long}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deactivate(long id, CancellationToken ct)
    {
        await movies.DeactivateAsync(id, ct);
        return NoContent();
    }

    /// <summary>Повернути на афішу.</summary>
    [HttpPost("{id:long}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Activate(long id, CancellationToken ct)
    {
        await movies.ActivateAsync(id, ct);
        return NoContent();
    }

    /// <summary>Додати жанр фільму (M:N з власними полями AddedAt, IsPrimary).</summary>
    [HttpPost("{id:long}/genres")]
    [ProducesResponseType<MovieDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MovieDto>> AddGenre(long id, AddMovieGenreDto input, CancellationToken ct)
        => Ok(await movies.AddGenreAsync(id, input, ct));

    /// <summary>Прибрати жанр фільму.</summary>
    [HttpDelete("{id:long}/genres/{genreId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveGenre(long id, int genreId, CancellationToken ct)
    {
        await movies.RemoveGenreAsync(id, genreId, ct);
        return NoContent();
    }

    /// <summary>Видалити фільм без сеансів (з сеансами → 409).</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await movies.DeleteAsync(id, ct);
        return NoContent();
    }
}
