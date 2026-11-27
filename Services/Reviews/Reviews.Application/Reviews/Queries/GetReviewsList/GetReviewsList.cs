using FluentValidation;
using MediatR;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Messaging;
using Reviews.Application.Common.Models;
using Reviews.Application.Reviews.Commands.CreateReview;

namespace Reviews.Application.Reviews.Queries.GetReviewsList;

/// <summary>Список відгуків: фільтри movieId, customerId, type, minRating, search (text-індекс); sortBy: createdAt | rating.</summary>
public sealed record GetReviewsListQuery(
    long? MovieId = null, long? CustomerId = null, string? Type = null, int? MinRating = null, string? Search = null,
    string SortBy = "createdAt", string SortDir = "desc", int Page = 1, int PageSize = Paging.DefaultPageSize)
    : IQuery<PagedResult<ReviewListItemDto>>;

public sealed class GetReviewsListQueryValidator : AbstractValidator<GetReviewsListQuery>
{
    public static readonly string[] SortFields = ["createdAt", "rating"];

    public GetReviewsListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
        RuleFor(x => x.MovieId).GreaterThan(0).When(x => x.MovieId is not null);
        RuleFor(x => x.CustomerId).GreaterThan(0).When(x => x.CustomerId is not null);
        RuleFor(x => x.Type).Must(t => CreateReviewCommandValidator.Types.Contains(t)).When(x => x.Type is not null)
            .WithMessage("Type must be one of: viewer, critic, quick");
        RuleFor(x => x.MinRating).InclusiveBetween(1, 5).When(x => x.MinRating is not null);
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.SortBy).Must(s => SortFields.Contains(s)).WithMessage("sortBy must be one of: createdAt, rating");
        RuleFor(x => x.SortDir).Must(d => d is "asc" or "desc").WithMessage("sortDir must be 'asc' or 'desc'");
    }
}

public sealed class GetReviewsListQueryHandler(IReviewQueries queries)
    : IRequestHandler<GetReviewsListQuery, PagedResult<ReviewListItemDto>>
{
    public Task<PagedResult<ReviewListItemDto>> Handle(GetReviewsListQuery q, CancellationToken ct)
        => queries.GetListAsync(new ReviewListFilter(
            q.MovieId, q.CustomerId, q.Type, q.MinRating, q.Search, q.SortBy, q.SortDir == "desc",
            q.Page, Math.Clamp(q.PageSize, 1, Paging.MaxPageSize)), ct);
}
