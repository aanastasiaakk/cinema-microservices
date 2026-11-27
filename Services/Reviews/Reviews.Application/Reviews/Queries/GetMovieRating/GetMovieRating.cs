using FluentValidation;
using MediatR;
using Reviews.Application.Caching;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Messaging;
using Reviews.Application.Common.Models;
using Reviews.Domain.Exceptions;

namespace Reviews.Application.Reviews.Queries.GetMovieRating;

/// <summary>Зведений рейтинг фільму (aggregation pipeline), кешується на 30 с і скидається командами.</summary>
public sealed record GetMovieRatingQuery(long MovieId) : IQuery<MovieRatingDto>, ICacheableQuery
{
    public string CacheKey => CacheKeys.MovieRating(MovieId);
    public TimeSpan Expiration => TimeSpan.FromSeconds(30);
}

public sealed class GetMovieRatingQueryValidator : AbstractValidator<GetMovieRatingQuery>
{
    public GetMovieRatingQueryValidator() => RuleFor(x => x.MovieId).GreaterThan(0);
}

public sealed class GetMovieRatingQueryHandler(IReviewQueries queries) : IRequestHandler<GetMovieRatingQuery, MovieRatingDto>
{
    public async Task<MovieRatingDto> Handle(GetMovieRatingQuery q, CancellationToken ct)
        => await queries.GetMovieRatingAsync(q.MovieId, ct) ?? throw new NotFoundException("Movie rating", q.MovieId);
}
