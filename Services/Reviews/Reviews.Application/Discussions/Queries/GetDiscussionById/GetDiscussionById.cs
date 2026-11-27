using FluentValidation;
using MediatR;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Messaging;
using Reviews.Application.Common.Models;
using Reviews.Application.Common.Validation;
using Reviews.Domain.Exceptions;

namespace Reviews.Application.Discussions.Queries.GetDiscussionById;

public sealed record GetDiscussionByIdQuery(string Id) : IQuery<DiscussionDto>;

public sealed class GetDiscussionByIdQueryValidator : AbstractValidator<GetDiscussionByIdQuery>
{
    public GetDiscussionByIdQueryValidator() => RuleFor(x => x.Id).MustBeObjectId();
}

public sealed class GetDiscussionByIdQueryHandler(IDiscussionQueries queries) : IRequestHandler<GetDiscussionByIdQuery, DiscussionDto>
{
    public async Task<DiscussionDto> Handle(GetDiscussionByIdQuery q, CancellationToken ct)
        => await queries.GetByIdAsync(q.Id, ct) ?? throw new NotFoundException("Discussion", q.Id);
}
