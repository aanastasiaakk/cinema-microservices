using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Reviews.Application.Common.Models;
using Reviews.Application.Reviews.Commands.AddComment;
using Reviews.Application.Reviews.Commands.CreateReview;
using Reviews.Application.Reviews.Commands.DeleteReview;
using Reviews.Application.Reviews.Commands.UpdateReview;
using Reviews.Application.Reviews.Queries.GetMovieRating;
using Reviews.Application.Reviews.Queries.GetReviewById;
using Reviews.Application.Reviews.Queries.GetReviewsList;
using Reviews.WebApi.Contracts;

namespace Reviews.WebApi.Controllers;

/// <summary>Відгуки на фільми.</summary>
[Route("api/reviews")]
public sealed class ReviewsController(IMapper mapper) : ApiControllerBase
{
    /// <summary>Список відгуків: фільтри movieId, customerId, type, minRating, search (text-індекс); sortBy createdAt | rating; пагінація в MongoDB.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ReviewListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ReviewListItemDto>>> GetList(
        [FromQuery] long? movieId, [FromQuery] long? customerId, [FromQuery] string? type, [FromQuery] int? minRating,
        [FromQuery] string? search, [FromQuery] string sortBy = "createdAt", [FromQuery] string sortDir = "desc",
        [FromQuery] int page = 1, [FromQuery] int pageSize = Paging.DefaultPageSize, CancellationToken ct = default)
    {
        var result = await Sender.Send(
            new GetReviewsListQuery(movieId, customerId, type, minRating, search, sortBy, sortDir, page, pageSize), ct);
        Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
        return Ok(result);
    }

    /// <summary>Відгук за Id (рядок ObjectId). Версія документа — у заголовку ETag.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType<ReviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewDto>> GetById(string id, CancellationToken ct)
    {
        var review = await Sender.Send(new GetReviewByIdQuery(id), ct);
        SetETag(review.Version);
        return Ok(review);
    }

    /// <summary>Створити відгук → 201 + Location /api/reviews/{ObjectId}. Повторний відгук глядача на фільм → 400 (валідатор) або 409 (unique-індекс).</summary>
    [HttpPost]
    [ProducesResponseType<ReviewDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReviewDto>> Create(CreateReviewRequest request, CancellationToken ct)
    {
        var created = await Sender.Send(mapper.Map<CreateReviewCommand>(request), ct);
        SetETag(created.Version);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Змінити оцінку й текст. If-Match: "версія" — інакше конфлікт версій 409.</summary>
    [HttpPut("{id}")]
    [ProducesResponseType<ReviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReviewDto>> Update(string id, UpdateReviewRequest request, CancellationToken ct)
    {
        var version = IfMatchVersion();
        var command = mapper.Map<UpdateReviewCommand>(request, o => { o.Items["id"] = id; o.Items["version"] = version!; });
        var updated = await Sender.Send(command, ct);
        SetETag(updated.Version);
        return Ok(updated);
    }

    /// <summary>Видалити (м'яко) відгук. Необов'язковий If-Match.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await Sender.Send(new DeleteReviewCommand(id, IfMatchVersion()), ct);
        return NoContent();
    }

    /// <summary>Додати коментар (вкладений масив comments).</summary>
    [HttpPost("{id}/comments")]
    [ProducesResponseType<ReviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReviewDto>> AddComment(string id, AddCommentRequest request, CancellationToken ct)
    {
        var updated = await Sender.Send(new AddCommentCommand(id, request.AuthorName, request.Text), ct);
        SetETag(updated.Version);
        return Ok(updated);
    }
}

/// <summary>Рейтинг фільму (aggregation pipeline), кешується.</summary>
[Route("api/movies")]
public sealed class MovieRatingsController : ApiControllerBase
{
    [HttpGet("{movieId:long}/rating")]
    [ProducesResponseType<MovieRatingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MovieRatingDto>> GetRating(long movieId, CancellationToken ct)
        => Ok(await Sender.Send(new GetMovieRatingQuery(movieId), ct));
}
