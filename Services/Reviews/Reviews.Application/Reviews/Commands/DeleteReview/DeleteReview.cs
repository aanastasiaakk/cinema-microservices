using Microsoft.Extensions.Logging;
using Reviews.Application.Common.Diagnostics;
using FluentValidation;
using MediatR;
using MongoDB.Bson;
using Reviews.Application.Caching;
using Reviews.Application.Common.Messaging;
using Reviews.Application.Common.Validation;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;

namespace Reviews.Application.Reviews.Commands.DeleteReview;

/// <summary>М'яке видалення відгуку (isDeleted = true).</summary>
public sealed record DeleteReviewCommand(string Id, int? ExpectedVersion) : ICommand;

public sealed class DeleteReviewCommandValidator : AbstractValidator<DeleteReviewCommand>
{
    public DeleteReviewCommandValidator()
    {
        RuleFor(x => x.Id).MustBeObjectId();
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).When(x => x.ExpectedVersion is not null);
    }
}

public sealed class DeleteReviewCommandHandler(IReviewRepository reviews, ICacheInvalidator cache,
                                               ILogger<DeleteReviewCommandHandler> logger)
    : IRequestHandler<DeleteReviewCommand>
{
    public async Task Handle(DeleteReviewCommand cmd, CancellationToken ct)
    {
        using var activity = ReviewsTelemetry.Source.StartActivity("DeleteReview");
        activity?.SetTag("review.id", cmd.Id);
        var review = await reviews.GetByIdAsync(ObjectId.Parse(cmd.Id), ct);
        if (review is null || review.IsDeleted) throw new NotFoundException("Review", cmd.Id);

        var expected = cmd.ExpectedVersion ?? review.Version;
        if (expected != review.Version)
            throw new ConflictException($"Review {cmd.Id} has version {review.Version}, but {expected} was expected");

        review.Delete();
        await reviews.UpdateAsync(review, expected, ct);
        cache.Invalidate(CacheKeys.MovieRating(review.MovieId));
        logger.LogInformation("Review {ReviewId} for movie {MovieId} deleted (soft), status {Status}", cmd.Id, review.MovieId, "Deleted");
    }
}
