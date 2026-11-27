using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.ValueObjects;
using Reviews.Infrastructure.Persistence;
using Reviews.Infrastructure.Seeding;

namespace Reviews.Tests;

/// <summary>
/// Інтеграційні тести на справжньому MongoDB. Запускаються, лише якщо задано REVIEWS_TEST_MONGO
/// (рядок підключення); кожен тест працює в окремій тимчасовій БД, яку потім видаляє.
/// </summary>
public sealed class MongoFactAttribute : FactAttribute
{
    public static string? ConnectionString => Environment.GetEnvironmentVariable("REVIEWS_TEST_MONGO");
    public MongoFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString)) Skip = "Set REVIEWS_TEST_MONGO to run MongoDB integration tests";
    }
}

public sealed class MongoIntegrationTests : IAsyncLifetime
{
    private MongoDbContext _context = null!;
    private string _db = "";

    public Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(MongoFactAttribute.ConnectionString)) return Task.CompletedTask;
        _db = $"reviews_test_{Guid.NewGuid():N}";
        _context = new MongoDbContext(Options.Create(new MongoDbOptions
        {
            ConnectionString = MongoFactAttribute.ConnectionString!, DatabaseName = _db
        }));
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrEmpty(_db)) await _context.Client.DropDatabaseAsync(_db);
    }

    private async Task<IndexCreationService> IndexesAsync()
    {
        var service = new IndexCreationService(_context, Options.Create(new MongoDbOptions()), NullLogger<IndexCreationService>.Instance);
        await service.CreateIndexesAsync();
        return service;
    }

    [MongoFact]
    public async Task Indexes_are_created_compound_text_and_unique()
    {
        await IndexesAsync();
        await IndexesAsync();                                                     // повторно — безпечно

        var indexes = await (await _context.Reviews.Indexes.ListAsync()).ToListAsync();
        var byName = indexes.ToDictionary(i => i["name"].AsString);
        Assert.Contains("ix_reviews_movie_createdAt", byName.Keys);
        Assert.True(byName["ux_reviews_movie_author_viewer"]["unique"].AsBoolean);
        Assert.Equal("text", byName["tx_reviews_text"]["key"]["_fts"].AsString);
    }

    [MongoFact]
    public async Task Seeding_is_idempotent()
    {
        await IndexesAsync();
        var reviews = new ReviewSeeder(_context);
        var discussions = new DiscussionSeeder(_context);

        Assert.Equal(5, await reviews.SeedAsync());
        Assert.Equal(2, await discussions.SeedAsync());
        Assert.Equal(0, await reviews.SeedAsync());                                // повторний запуск
        Assert.Equal(0, await discussions.SeedAsync());
        Assert.Equal(5, await _context.Reviews.CountDocumentsAsync(FilterDefinition<Review>.Empty));
    }

    [MongoFact]
    public async Task Stored_document_has_lab01_shape()
    {
        await new ReviewSeeder(_context).SeedAsync();
        var raw = await _context.Database.GetCollection<BsonDocument>("reviews")
            .Find(new BsonDocument("_id", ObjectId.Parse("6750c0000000000000000001"))).FirstAsync();

        Assert.Equal("viewer", raw["type"].AsString);
        Assert.Equal(1L, raw["author"]["customerId"].AsInt64);
        Assert.Equal(2, raw["comments"].AsBsonArray.Count);
        Assert.Equal("authorName", raw["comments"][0].AsBsonDocument.Names.First());
        Assert.DoesNotContain(raw.Names, n => n.StartsWith('_') && n != "_id");
    }

    [MongoFact]
    public async Task Update_with_stale_version_is_a_conflict()
    {
        var repo = new ReviewRepository(_context);
        var review = new Review(ReviewType.Viewer, 1, "Довбуш", new Author(9, "Тест"), new Rating(4), "Текст");
        await repo.AddAsync(review);

        var first = (await repo.GetByIdAsync(review.Id))!;
        var second = (await repo.GetByIdAsync(review.Id))!;

        first.Edit(new Rating(5), "Перша зміна", null);
        await repo.UpdateAsync(first, expectedVersion: 1);
        Assert.Equal(2, first.Version);

        second.Edit(new Rating(1), "Застаріла зміна", null);
        await Assert.ThrowsAsync<ConflictException>(() => repo.UpdateAsync(second, expectedVersion: 1));

        var stored = (await repo.GetByIdAsync(review.Id))!;
        Assert.Equal("Перша зміна", stored.Text);
        Assert.Equal(2, stored.Version);
    }

    [MongoFact]
    public async Task Update_preserves_fields_unknown_to_the_domain()
    {
        await new ReviewSeeder(_context).SeedAsync();
        var raw = _context.Database.GetCollection<BsonDocument>("reviews");
        var id = ObjectId.Parse("6750c0000000000000000003");
        await raw.UpdateOneAsync(new BsonDocument("_id", id), new BsonDocument("$set", new BsonDocument("pros", new BsonArray { "візуал" })));

        var repo = new ReviewRepository(_context);
        var review = (await repo.GetByIdAsync(id))!;
        review.Edit(new Rating(4), "Оновлено", null);
        await repo.UpdateAsync(review, review.Version);

        var doc = await raw.Find(new BsonDocument("_id", id)).FirstAsync();
        Assert.Equal("візуал", doc["pros"][0].AsString);                           // гнучка схема збережена
        Assert.Equal(4, doc["rating"].AsInt32);
    }

    [MongoFact]
    public async Task Second_viewer_review_hits_unique_index()
    {
        await IndexesAsync();
        var repo = new ReviewRepository(_context);
        await repo.AddAsync(new Review(ReviewType.Viewer, 2, "Мавка", new Author(5, "Тест"), new Rating(4), "Раз"));

        Assert.True(await repo.ExistsAsync(2, 5));
        var ex = await Assert.ThrowsAsync<MongoWriteException>(() =>
            repo.AddAsync(new Review(ReviewType.Viewer, 2, "Мавка", new Author(5, "Тест"), new Rating(2), "Два")));
        Assert.Equal(ServerErrorCategory.DuplicateKey, ex.WriteError.Category);
    }

    [MongoFact]
    public async Task Queries_page_in_mongo_and_project_into_dto()
    {
        await IndexesAsync();
        await new ReviewSeeder(_context).SeedAsync();
        var queries = new ReviewQueries(_context);

        var page = await queries.GetListAsync(new Reviews.Application.Common.Interfaces.ReviewListFilter(
            null, null, null, null, null, "rating", true, Page: 2, PageSize: 2), default);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(3, page.TotalPages);

        var found = await queries.GetListAsync(new Reviews.Application.Common.Interfaces.ReviewListFilter(
            null, null, null, null, "Карпат", "createdAt", true, 1, 10), default);
        Assert.Single(found.Items);                                                // text-індекс

        var details = await queries.GetByIdAsync("6750c0000000000000000001", default);
        Assert.Equal(2, details!.CommentsCount);
        Assert.Equal("6750a0000000000000000001", details.DiscussionId);

        var rating = await queries.GetMovieRatingAsync(3, default);
        Assert.Equal(2, rating!.Count);
        Assert.Equal(4.0, rating.Average);
    }
}
