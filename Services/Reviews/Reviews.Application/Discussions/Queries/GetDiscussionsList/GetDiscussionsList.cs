using FluentValidation;
using MediatR;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Messaging;
using Reviews.Application.Common.Models;

namespace Reviews.Application.Discussions.Queries.GetDiscussionsList;

/// <summary>Теми фільму або тегу, найактивніші першими.</summary>
public sealed record GetDiscussionsListQuery(long? MovieId = null, string? Tag = null, int Page = 1, int PageSize = Paging.DefaultPageSize)
    : IQuery<PagedResult<DiscussionDto>>;

public sealed class GetDiscussionsListQueryValidator : AbstractValidator<GetDiscussionsListQuery>
{
    public GetDiscussionsListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
        RuleFor(x => x.MovieId).GreaterThan(0).When(x => x.MovieId is not null);
        RuleFor(x => x.Tag).MaximumLength(30);
    }
}

public sealed class GetDiscussionsListQueryHandler(IDiscussionQueries queries)
    : IRequestHandler<GetDiscussionsListQuery, PagedResult<DiscussionDto>>
{
    public Task<PagedResult<DiscussionDto>> Handle(GetDiscussionsListQuery q, CancellationToken ct)
        => queries.GetListAsync(q.MovieId, q.Tag?.Trim().ToLowerInvariant(), q.Page, Math.Clamp(q.PageSize, 1, Paging.MaxPageSize), ct);
}
