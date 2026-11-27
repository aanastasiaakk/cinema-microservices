using Reviews.Application.Common.Models;

namespace Reviews.Application.Common.Interfaces;

/// <summary>Параметри списку відгуків (фільтри → $match, сортування за дозволеними полями, skip/limit).</summary>
public sealed record ReviewListFilter(
    long? MovieId, long? CustomerId, string? Type, int? MinRating, string? Search,
    string SortBy, bool Descending, int Page, int PageSize);

/// <summary>
/// Інтерфейс ЧИТАННЯ (CQRS read side), як IOrderQueries в eShop. Оголошений в Application,
/// реалізований в Infrastructure проєкціями й aggregation pipeline — обробники запитів не знають про MongoDB.
/// </summary>
public interface IReviewQueries
{
    Task<ReviewDto?> GetByIdAsync(string id, CancellationToken ct);
    Task<PagedResult<ReviewListItemDto>> GetListAsync(ReviewListFilter filter, CancellationToken ct);
    Task<MovieRatingDto?> GetMovieRatingAsync(long movieId, CancellationToken ct);
}

public interface IDiscussionQueries
{
    Task<DiscussionDto?> GetByIdAsync(string id, CancellationToken ct);
    Task<PagedResult<DiscussionDto>> GetListAsync(long? movieId, string? tag, int page, int pageSize, CancellationToken ct);
}
