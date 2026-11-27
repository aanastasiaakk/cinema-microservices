using Microsoft.Extensions.Logging;
using Reviews.Application.Common.Diagnostics;
using AutoMapper;
using FluentValidation;
using MediatR;
using Reviews.Application.Caching;
using Reviews.Application.Common.Messaging;
using Reviews.Application.Common.Models;
using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews.Commands.CreateReview;

/// <summary>Створити відгук. Type: viewer | critic | quick.</summary>
public sealed record CreateReviewCommand(
    string Type, long MovieId, string MovieTitle, long CustomerId, string AuthorName, int Rating, string? Text,
    string? CriticOutlet = null, int? CriticScore100 = null, bool? VerifiedViewer = null, bool? ContainsSpoilers = null)
    : ICommand<ReviewDto>;

public sealed class CreateReviewCommandValidator : AbstractValidator<CreateReviewCommand>
{
    public static readonly string[] Types = ["viewer", "critic", "quick"];

    public CreateReviewCommandValidator(IReviewRepository reviews)
    {
        RuleFor(x => x.Type).Must(t => Types.Contains(t)).WithMessage("Type must be one of: viewer, critic, quick");
        RuleFor(x => x.MovieId).GreaterThan(0);
        RuleFor(x => x.MovieTitle).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.AuthorName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Rating).InclusiveBetween(Rating.Min, Rating.Max);
        RuleFor(x => x.Text).NotEmpty().When(x => x.Type != "quick")
            .WithMessage("Text is required for viewer and critic reviews");
        RuleFor(x => x.Text).MaximumLength(Review.MaxTextLength);
        RuleFor(x => x.CriticOutlet).NotEmpty().When(x => x.Type == "critic")
            .WithMessage("CriticOutlet is required for critic reviews");
        RuleFor(x => x.CriticOutlet).Empty().When(x => x.Type != "critic")
            .WithMessage("CriticOutlet is allowed only for critic reviews");
        RuleFor(x => x.CriticScore100).InclusiveBetween(0, 100).When(x => x.CriticScore100 is not null);

        // асинхронне правило із запитом до MongoDB: один відгук глядача на фільм
        RuleFor(x => x)
            .MustAsync(async (cmd, ct) => !await reviews.ExistsAsync(cmd.MovieId, cmd.CustomerId, ct))
            .When(x => x.Type == "viewer" && x.MovieId > 0 && x.CustomerId > 0)
            .WithName("CustomerId")
            .WithMessage("This customer has already reviewed this movie");
    }
}

public sealed class CreateReviewCommandHandler(IReviewRepository reviews, IMapper mapper, ICacheInvalidator cache,
                                               ILogger<CreateReviewCommandHandler> logger)
    : IRequestHandler<CreateReviewCommand, ReviewDto>
{
    public async Task<ReviewDto> Handle(CreateReviewCommand cmd, CancellationToken ct)
    {
        using var activity = ReviewsTelemetry.Source.StartActivity("CreateReview");
        activity?.SetTag("movie.id", cmd.MovieId);
        activity?.SetTag("review.type", cmd.Type);
        var type = Enum.Parse<ReviewType>(cmd.Type, ignoreCase: true);
        var review = new Review(
            type, cmd.MovieId, cmd.MovieTitle, new Author(cmd.CustomerId, cmd.AuthorName), new Rating(cmd.Rating), cmd.Text,
            type == ReviewType.Critic ? new CriticInfo(cmd.CriticOutlet!, cmd.CriticScore100) : null,
            cmd.VerifiedViewer, cmd.ContainsSpoilers);

        await reviews.AddAsync(review, ct);
        cache.Invalidate(CacheKeys.MovieRating(cmd.MovieId));
        activity?.SetTag("review.id", review.Id.ToString());
        logger.LogInformation("Review {ReviewId} ({ReviewType}) created for movie {MovieId} by customer {CustomerId} with rating {Rating}",
            review.Id.ToString(), cmd.Type, cmd.MovieId, cmd.CustomerId, cmd.Rating);
        return mapper.Map<ReviewDto>(review);
    }
}
