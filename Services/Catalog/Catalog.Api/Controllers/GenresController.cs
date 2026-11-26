using Catalog.Bll.Dto;
using Catalog.Bll.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

/// <summary>Жанри (довідник).</summary>
[ApiController]
[Route("api/genres")]
[Produces("application/json")]
public sealed class GenresController(IGenreService genres) : ControllerBase
{
    /// <summary>Усі жанри (кешуються в пам'яті).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<GenreDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GenreDto>>> GetAll(CancellationToken ct)
        => Ok(await genres.GetAllAsync(ct));

    /// <summary>Жанр з фільмами (Explicit Loading — два SQL-запити).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<GenreWithMoviesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GenreWithMoviesDto>> GetById(int id, CancellationToken ct)
        => Ok(await genres.GetWithMoviesAsync(id, ct));

    /// <summary>Фільми, яким жанр додано за останні N днів (LINQ через проміжну сутність M:N і її поле AddedAt).</summary>
    [HttpGet("{slug}/recent-movies")]
    [ProducesResponseType<IReadOnlyList<RecentGenreMovieDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RecentGenreMovieDto>>> GetRecentMovies(
        string slug, [FromQuery] int days = 30, CancellationToken ct = default)
        => Ok(await genres.GetRecentlyAddedMoviesAsync(slug, days, ct));

    /// <summary>Кількість фільмів у кожному жанрі.</summary>
    [HttpGet("stats")]
    [ProducesResponseType<IReadOnlyList<GenreStatsDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GenreStatsDto>>> GetStats(
        [FromQuery] bool primaryOnly = false, CancellationToken ct = default)
        => Ok(await genres.GetStatsAsync(primaryOnly, ct));

    /// <summary>Створити жанр. Дубль назви або slug → 409.</summary>
    [HttpPost]
    [ProducesResponseType<GenreDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GenreDto>> Create(
        CreateGenreDto input, [FromServices] IValidator<CreateGenreDto> validator, CancellationToken ct)
    {
        if (await this.ValidateAsync(validator, input, ct) is { } invalid) return invalid;
        var created = await genres.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
