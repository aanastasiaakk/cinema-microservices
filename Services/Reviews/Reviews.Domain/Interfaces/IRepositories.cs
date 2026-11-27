using MongoDB.Bson;
using Reviews.Domain.Entities;

namespace Reviews.Domain.Interfaces;

/// <summary>
/// Контракт запису для агрегату Review. Реалізація — в Infrastructure (MongoDB Driver).
/// UpdateAsync застосовує зміну лише до документа з очікуваною версією, інакше ConflictException.
/// </summary>
public interface IReviewRepository
{
    Task<Review?> GetByIdAsync(ObjectId id, CancellationToken ct = default);
    /// <summary>Чи є вже не видалений відгук глядача цього клієнта на цей фільм.</summary>
    Task<bool> ExistsAsync(long movieId, long customerId, CancellationToken ct = default);
    Task AddAsync(Review review, CancellationToken ct = default);
    Task UpdateAsync(Review review, int expectedVersion, CancellationToken ct = default);
    /// <summary>Подія MovieRenamed: updateMany за movieId (eventual consistency).</summary>
    Task<long> RenameMovieAsync(long movieId, string movieTitle, CancellationToken ct = default);
}

public interface IDiscussionRepository
{
    Task<Discussion?> GetByIdAsync(ObjectId id, CancellationToken ct = default);
    Task<bool> ExistsWithTitleAsync(long movieId, string title, CancellationToken ct = default);
    Task AddAsync(Discussion discussion, CancellationToken ct = default);
    Task UpdateAsync(Discussion discussion, int expectedVersion, CancellationToken ct = default);
}
