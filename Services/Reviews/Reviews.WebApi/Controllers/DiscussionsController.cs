using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Reviews.Application.Common.Models;
using Reviews.Application.Discussions.Commands.CloseDiscussion;
using Reviews.Application.Discussions.Commands.CreateDiscussion;
using Reviews.Application.Discussions.Commands.PostMessage;
using Reviews.Application.Discussions.Queries.GetDiscussionById;
using Reviews.Application.Discussions.Queries.GetDiscussionsList;
using Reviews.WebApi.Contracts;

namespace Reviews.WebApi.Controllers;

/// <summary>Обговорення фільмів.</summary>
[Route("api/discussions")]
public sealed class DiscussionsController(IMapper mapper) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<DiscussionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<DiscussionDto>>> GetList(
        [FromQuery] long? movieId, [FromQuery] string? tag, [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize, CancellationToken ct = default)
        => Ok(await Sender.Send(new GetDiscussionsListQuery(movieId, tag, page, pageSize), ct));

    [HttpGet("{id}")]
    [ProducesResponseType<DiscussionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DiscussionDto>> GetById(string id, CancellationToken ct)
    {
        var discussion = await Sender.Send(new GetDiscussionByIdQuery(id), ct);
        SetETag(discussion.Version);
        return Ok(discussion);
    }

    /// <summary>Створити тему → 201 + Location. Дубль назви для фільму → 400 (валідатор) / 409 (unique-індекс).</summary>
    [HttpPost]
    [ProducesResponseType<DiscussionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DiscussionDto>> Create(CreateDiscussionRequest request, CancellationToken ct)
    {
        var created = await Sender.Send(mapper.Map<CreateDiscussionCommand>(request), ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("{id}/messages")]
    [ProducesResponseType<DiscussionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DiscussionDto>> PostMessage(string id, PostMessageRequest request, CancellationToken ct)
        => Ok(await Sender.Send(new PostMessageCommand(id, request.CustomerId, request.AuthorName, request.Text), ct));

    [HttpPost("{id}/close")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Close(string id, CancellationToken ct)
    {
        await Sender.Send(new CloseDiscussionCommand(id, IfMatchVersion()), ct);
        return NoContent();
    }
}
