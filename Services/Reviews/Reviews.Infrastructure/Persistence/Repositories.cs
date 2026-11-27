using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Domain.Common;
using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;

namespace Reviews.Infrastructure.Persistence;

/// <summary>
/// Спільна логіка оновлення з ОПТИМІСТИЧНОЮ КОНКУРЕНТНІСТЮ: фільтр { _id, version: expected } і $set усіх
/// змаплених полів сутності (+ version = expected + 1). Невідомі полям домену елементи документа
/// (photos, pros, watchedFormat … — гнучка схема) лишаються недоторканими, бо документ не замінюється цілком.
/// Якщо документ уже змінив хтось інший — MatchedCount = 0 → ConflictException (409).
/// </summary>
internal static class VersionedUpdate
{
    public static async Task ApplyAsync<T>(IMongoCollection<T> collection, T entity, int expectedVersion,
                                           string[] optionalFields, string entityName, CancellationToken ct)
        where T : BaseEntity
    {
        var set = entity.ToBsonDocument();
        set.Remove("_id");
        set.Remove("createdAt");
        set["version"] = expectedVersion + 1;

        var update = new BsonDocument("$set", set);
        var unset = new BsonDocument(optionalFields.Where(f => !set.Contains(f)).Select(f => new BsonElement(f, "")));
        if (unset.ElementCount > 0) update.Add("$unset", unset);

        var filter = Builders<T>.Filter.Eq(e => e.Id, entity.Id) & Builders<T>.Filter.Eq(e => e.Version, expectedVersion);
        var result = await collection.UpdateOneAsync(filter, update, cancellationToken: ct);

        if (result.MatchedCount == 0)
        {
            var exists = await collection.Find(Builders<T>.Filter.Eq(e => e.Id, entity.Id)).AnyAsync(ct);
            if (!exists) throw new NotFoundException(entityName, entity.Id);
            throw new ConflictException($"{entityName} {entity.Id} was modified by another request (expected version {expectedVersion})");
        }
        entity.BumpVersion();
    }
}

public sealed class ReviewRepository(MongoDbContext context) : IReviewRepository
{
    private static readonly string[] OptionalFields = ["text", "criticInfo", "verifiedViewer", "containsSpoilers", "discussionId", "updatedAt"];
    private readonly IMongoCollection<Review> _reviews = context.Reviews;

    public async Task<Review?> GetByIdAsync(ObjectId id, CancellationToken ct = default)
        => await _reviews.Find(r => r.Id == id).FirstOrDefaultAsync(ct);

    /// <summary>Будь-який (навіть видалений) відгук глядача цього клієнта на фільм — як і унікальний індекс.</summary>
    public Task<bool> ExistsAsync(long movieId, long customerId, CancellationToken ct = default)
        => _reviews.Find(r => r.MovieId == movieId && r.Author.CustomerId == customerId && r.Type == ReviewType.Viewer)
                   .AnyAsync(ct);

    public Task AddAsync(Review review, CancellationToken ct = default)
        => _reviews.InsertOneAsync(review, cancellationToken: ct);

    public Task UpdateAsync(Review review, int expectedVersion, CancellationToken ct = default)
        => VersionedUpdate.ApplyAsync(_reviews, review, expectedVersion, OptionalFields, "Review", ct);

    public async Task<long> RenameMovieAsync(long movieId, string movieTitle, CancellationToken ct = default)
    {
        var result = await _reviews.UpdateManyAsync(
            r => r.MovieId == movieId,
            Builders<Review>.Update.Set(r => r.MovieTitle, movieTitle).Inc(r => r.Version, 1)
                                   .CurrentDate(r => r.UpdatedAt),
            cancellationToken: ct);
        return result.ModifiedCount;
    }
}

public sealed class DiscussionRepository(MongoDbContext context) : IDiscussionRepository
{
    private static readonly string[] OptionalFields = ["lastMessageAt", "updatedAt"];
    private readonly IMongoCollection<Discussion> _discussions = context.Discussions;

    public async Task<Discussion?> GetByIdAsync(ObjectId id, CancellationToken ct = default)
        => await _discussions.Find(d => d.Id == id).FirstOrDefaultAsync(ct);

    public Task<bool> ExistsWithTitleAsync(long movieId, string title, CancellationToken ct = default)
        => _discussions.Find(d => d.MovieId == movieId && d.Title == title).AnyAsync(ct);

    public Task AddAsync(Discussion discussion, CancellationToken ct = default)
        => _discussions.InsertOneAsync(discussion, cancellationToken: ct);

    public Task UpdateAsync(Discussion discussion, int expectedVersion, CancellationToken ct = default)
        => VersionedUpdate.ApplyAsync(_discussions, discussion, expectedVersion, OptionalFields, "Discussion", ct);
}
