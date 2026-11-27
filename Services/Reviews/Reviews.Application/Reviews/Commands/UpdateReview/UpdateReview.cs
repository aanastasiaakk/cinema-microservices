using AutoMapper;
using FluentValidation;
using MediatR;
using MongoDB.Bson;
using Reviews.Application.Caching;
using Reviews.Application.Common.Messaging;
using Reviews.Application.Common.Models;
using Reviews.Application.Common.Validation;
using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews.Commands.UpdateReview;

/// <summary>Змінити оцінку/текст. ExpectedVersion — з заголовка If-Match (оптимістична конкурентність).</summary>
public sealed record UpdateReviewCommand(string Id, int Rating, string? Text, bool? ContainsSpoilers, int? ExpectedVersion)
    : ICommand<ReviewDto>;

public sealed class UpdateReviewCommandValidator : AbstractValidator<UpdateReviewCommand>
{
    public UpdateReviewCommandValidator()
    {
        RuleFor(x => x.Id).MustBeObjectId();
        RuleFor(x => x.Rating).InclusiveBetween(Rating.Min, Rating.Max);
        RuleFor(x => x.Text).MaximumLength(Review.MaxTextLength);
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).When(x => x.ExpectedVersion is not null);
    }
}

public sealed class UpdateReviewCommandHandler(IReviewRepository reviews, IMapper mapper, ICacheInvalidator cache)
    : IRequestHandler<UpdateReviewCommand, ReviewDto>
{
    public async Task<ReviewDto> Handle(UpdateReviewCommand cmd, CancellationToken ct)
    {
        var review = await reviews.GetByIdAsync(ObjectId.Parse(cmd.Id), ct);
        if (review is null || review.IsDeleted) throw new NotFoundException("Review", cmd.Id);

        // клієнт редагував стару версію → конфлікт одразу; інакше репозиторій перевірить версію атомарно
        var expected = cmd.ExpectedVersion ?? review.Version;
        if (expected != review.Version)
            throw new ConflictException($"Review {cmd.Id} has version {review.Version}, but {expected} was expected");

        review.Edit(new Rating(cmd.Rating), cmd.Text, cmd.ContainsSpoilers);
        await reviews.UpdateAsync(review, expected, ct);
        cache.Invalidate(CacheKeys.MovieRating(review.MovieId));
        return mapper.Map<ReviewDto>(review);
    }
}
