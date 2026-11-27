using FluentValidation;
using MediatR;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Messaging;
using Reviews.Application.Common.Models;
using Reviews.Application.Common.Validation;
using Reviews.Domain.Exceptions;

namespace Reviews.Application.Reviews.Queries.GetReviewById;

public sealed record GetReviewByIdQuery(string Id) : IQuery<ReviewDto>;

public sealed class GetReviewByIdQueryValidator : AbstractValidator<GetReviewByIdQuery>
{
    public GetReviewByIdQueryValidator() => RuleFor(x => x.Id).MustBeObjectId();
}

/// <summary>Обробник запиту не звертається до MongoDB напряму — лише через IReviewQueries.</summary>
public sealed class GetReviewByIdQueryHandler(IReviewQueries queries) : IRequestHandler<GetReviewByIdQuery, ReviewDto>
{
    public async Task<ReviewDto> Handle(GetReviewByIdQuery q, CancellationToken ct)
        => await queries.GetByIdAsync(q.Id, ct) ?? throw new NotFoundException("Review", q.Id);
}
