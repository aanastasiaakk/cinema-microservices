using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Models;
using Reviews.Domain.Entities;

namespace Reviews.Infrastructure.Persistence;

/// <summary>
/// READ side (CQRS): aggregation pipeline $match → $sort → $skip → $limit → $project, результат одразу
/// десеріалізується в DTO — з БД вибираються лише потрібні поля, пагінація виконується в MongoDB.
/// </summary>
public sealed class ReviewQueries(MongoDbContext context) : IReviewQueries
{
    private readonly IMongoCollection<BsonDocument> _reviews =
        context.Database.GetCollection<BsonDocument>(MongoDbContext.ReviewsCollection);

    private static readonly BsonDocument CommentsCount =
        new("$size", new BsonDocument("$ifNull", new BsonArray { "$comments", new BsonArray() }));

    private static readonly BsonDocument ListProjection = new()
    {
        { "_id", 1 }, { "type", 1 }, { "movieId", 1 }, { "movieTitle", 1 }, { "author", 1 }, { "rating", 1 },
        { "text", 1 }, { "createdAt", 1 }, { "version", 1 }, { "commentsCount", CommentsCount }
    };

    private static readonly BsonDocument DetailsProjection = new()
    {
        { "_id", 1 }, { "type", 1 }, { "movieId", 1 }, { "movieTitle", 1 }, { "author", 1 }, { "rating", 1 },
        { "text", 1 }, { "criticInfo", 1 }, { "verifiedViewer", 1 }, { "containsSpoilers", 1 }, { "discussionId", 1 },
        { "comments", new BsonDocument("$ifNull", new BsonArray { "$comments", new BsonArray() }) },
        { "commentsCount", CommentsCount }, { "createdAt", 1 }, { "updatedAt", 1 }, { "version", 1 }
    };

    public async Task<ReviewDto?> GetByIdAsync(string id, CancellationToken ct)
        => await _reviews.Aggregate()
            .Match(new BsonDocument { { "_id", ObjectId.Parse(id) }, { "isDeleted", false } })
            .Project<ReviewDto>(DetailsProjection)
            .FirstOrDefaultAsync(ct);

    public async Task<PagedResult<ReviewListItemDto>> GetListAsync(ReviewListFilter f, CancellationToken ct)
    {
        var match = new BsonDocument("isDeleted", false);
        if (f.MovieId is not null) match["movieId"] = f.MovieId.Value;
        if (f.CustomerId is not null) match["author.customerId"] = f.CustomerId.Value;
        if (f.Type is not null) match["type"] = f.Type;
        if (f.MinRating is not null) match["rating"] = new BsonDocument("$gte", f.MinRating.Value);
        if (!string.IsNullOrWhiteSpace(f.Search)) match["$text"] = new BsonDocument("$search", f.Search);

        var sortField = f.SortBy == "rating" ? "rating" : "createdAt";
        var dir = f.Descending ? -1 : 1;
        var sort = new BsonDocument { { sortField, dir }, { "_id", dir } };   // _id — стабільний порядок сторінок

        var total = await _reviews.CountDocumentsAsync(match, cancellationToken: ct);
        var items = await _reviews.Aggregate()
            .Match(match)
            .Sort(sort)
            .Skip((f.Page - 1) * f.PageSize)
            .Limit(f.PageSize)
            .Project<ReviewListItemDto>(ListProjection)
            .ToListAsync(ct);

        return new PagedResult<ReviewListItemDto>(items, total, f.Page, f.PageSize);
    }

    /// <summary>Рейтинг фільму: $match (не видалені) → $group (середнє, кількість, розподіл оцінок).</summary>
    public async Task<MovieRatingDto?> GetMovieRatingAsync(long movieId, CancellationToken ct)
    {
        BsonDocument Count(int r) => new("$sum", new BsonDocument("$cond",
            new BsonArray { new BsonDocument("$eq", new BsonArray { "$rating", r }), 1, 0 }));

        var group = new BsonDocument
        {
            { "_id", "$movieId" },
            { "movieTitle", new BsonDocument("$first", "$movieTitle") },
            { "average", new BsonDocument("$avg", "$rating") },
            { "count", new BsonDocument("$sum", 1) },
            { "r1", Count(1) }, { "r2", Count(2) }, { "r3", Count(3) }, { "r4", Count(4) }, { "r5", Count(5) }
        };

        var row = await _reviews.Aggregate()
            .Match(new BsonDocument { { "movieId", movieId }, { "isDeleted", false } })
            .Group(group)
            .FirstOrDefaultAsync(ct);
        if (row is null) return null;

        var distribution = Enumerable.Range(1, 5).ToDictionary(i => i.ToString(), i => row[$"r{i}"].ToInt32());
        return new MovieRatingDto(movieId, row["movieTitle"].AsString, Math.Round(row["average"].ToDouble(), 2),
                                  row["count"].ToInt32(), distribution);
    }
}

public sealed class DiscussionQueries(MongoDbContext context) : IDiscussionQueries
{
    private readonly IMongoCollection<BsonDocument> _discussions =
        context.Database.GetCollection<BsonDocument>(MongoDbContext.DiscussionsCollection);

    private static readonly BsonDocument Projection = new()
    {
        { "_id", 1 }, { "movieId", 1 }, { "movieTitle", 1 }, { "title", 1 }, { "createdBy", 1 }, { "status", 1 },
        { "tags", new BsonDocument("$ifNull", new BsonArray { "$tags", new BsonArray() }) },
        { "recentMessages", new BsonDocument("$ifNull", new BsonArray { "$recentMessages", new BsonArray() }) },
        { "messagesCount", 1 }, { "lastMessageAt", 1 }, { "createdAt", 1 },
        { "version", new BsonDocument("$ifNull", new BsonArray { "$version", 1 }) }
    };

    public async Task<DiscussionDto?> GetByIdAsync(string id, CancellationToken ct)
        => await _discussions.Aggregate()
            .Match(new BsonDocument("_id", ObjectId.Parse(id)))
            .Project<DiscussionDto>(Projection)
            .FirstOrDefaultAsync(ct);

    public async Task<PagedResult<DiscussionDto>> GetListAsync(long? movieId, string? tag, int page, int pageSize, CancellationToken ct)
    {
        var match = new BsonDocument();
        if (movieId is not null) match["movieId"] = movieId.Value;
        if (!string.IsNullOrWhiteSpace(tag)) match["tags"] = tag;      // multikey-індекс ix_discussions_tags

        var total = await _discussions.CountDocumentsAsync(match, cancellationToken: ct);
        var items = await _discussions.Aggregate()
            .Match(match)
            .Sort(new BsonDocument { { "lastMessageAt", -1 }, { "_id", -1 } })
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .Project<DiscussionDto>(Projection)
            .ToListAsync(ct);
        return new PagedResult<DiscussionDto>(items, total, page, pageSize);
    }
}
