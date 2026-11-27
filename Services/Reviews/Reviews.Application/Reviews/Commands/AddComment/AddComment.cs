using AutoMapper;
using FluentValidation;
using MediatR;
using MongoDB.Bson;
using Reviews.Application.Common.Messaging;
using Reviews.Application.Common.Models;
using Reviews.Application.Common.Validation;
using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;

namespace Reviews.Application.Reviews.Commands.AddComment;

/// <summary>Додати коментар у вкладений масив comments відгуку.</summary>
public sealed record AddCommentCommand(string ReviewId, string AuthorName, string Text) : ICommand<ReviewDto>;

public sealed class AddCommentCommandValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentCommandValidator()
    {
        RuleFor(x => x.ReviewId).MustBeObjectId();
        RuleFor(x => x.AuthorName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Text).NotEmpty().MaximumLength(Comment.MaxLength);
    }
}

public sealed class AddCommentCommandHandler(IReviewRepository reviews, IMapper mapper, TimeProvider clock)
    : IRequestHandler<AddCommentCommand, ReviewDto>
{
    public async Task<ReviewDto> Handle(AddCommentCommand cmd, CancellationToken ct)
    {
        var review = await reviews.GetByIdAsync(ObjectId.Parse(cmd.ReviewId), ct);
        if (review is null || review.IsDeleted) throw new NotFoundException("Review", cmd.ReviewId);

        var expected = review.Version;
        review.AddComment(cmd.AuthorName, cmd.Text, clock.GetUtcNow().UtcDateTime);
        await reviews.UpdateAsync(review, expected, ct);
        return mapper.Map<ReviewDto>(review);
    }
}
