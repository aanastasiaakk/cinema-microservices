using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Domain.Entities;
using Reviews.Domain.ValueObjects;
using Reviews.Infrastructure.Persistence;

namespace Reviews.Infrastructure.Seeding;

/// <summary>Сідер однієї колекції. Ідемпотентний: якщо дані вже є — нічого не робить.</summary>
public interface IDataSeeder
{
    string Collection { get; }
    Task<int> SeedAsync(CancellationToken ct = default);
}

/// <summary>Відгуки з db/p3/seed.js (ті самі _id, movieId, customerId), вставка bulk-операцією InsertMany.</summary>
public sealed class ReviewSeeder(MongoDbContext context) : IDataSeeder
{
    private static ObjectId Rev(int n) => ObjectId.Parse($"6750c00000000000000000{n:00}");
    public static readonly ObjectId DiscussionDovbush = ObjectId.Parse("6750a0000000000000000001");

    public string Collection => MongoDbContext.ReviewsCollection;

    public async Task<int> SeedAsync(CancellationToken ct = default)
    {
        if (await context.Reviews.EstimatedDocumentCountAsync(cancellationToken: ct) > 0) return 0;   // уже наповнено

        var olena = new Author(1, "Олена Коваль");
        var andrii = new Author(2, "Андрій Мельник");
        var iryna = new Author(3, "Ірина Бондар");

        var r1 = Review.Restore(Rev(1), D("2026-11-19T10:00:00Z"), ReviewType.Viewer, 1, "Довбуш", olena, new Rating(5),
            "Неймовірні краєвиди Карпат і сильна акторська гра.", verifiedViewer: true);
        r1.AddComment("Андрій Мельник", "Згоден, музика теж топ.", D("2026-11-19T12:00:00Z"));
        r1.AddComment("Ірина Бондар", "Треба йти!", D("2026-11-19T13:10:00Z"));
        r1.LinkDiscussion(DiscussionDovbush);

        var r2 = Review.Restore(Rev(2), D("2026-11-20T08:00:00Z"), ReviewType.Quick, 1, "Довбуш", iryna, new Rating(4), null);

        var r3 = Review.Restore(Rev(3), D("2026-11-22T21:00:00Z"), ReviewType.Critic, 3, "Дюна: Частина друга", andrii,
            new Rating(5), "Масштабне продовження з бездоганним звуком.", new CriticInfo("Кіноблог КН", 92),
            containsSpoilers: false);

        var r4 = Review.Restore(Rev(4), D("2026-11-23T08:00:00Z"), ReviewType.Viewer, 2, "Мавка. Лісова пісня", andrii,
            new Rating(4), "Гарна анімація, фінал трохи передбачуваний.", verifiedViewer: false, containsSpoilers: true);
        r4.AddComment("Олена Коваль", "Діти були в захваті.", D("2026-11-23T09:00:00Z"));

        var r5 = Review.Restore(Rev(5), D("2026-11-24T20:00:00Z"), ReviewType.Viewer, 3, "Дюна: Частина друга", olena,
            new Rating(3), "Красиво, але задовго.", verifiedViewer: true);

        Review[] reviews = [r1, r2, r3, r4, r5];
        await context.Reviews.InsertManyAsync(reviews, cancellationToken: ct);      // bulk-вставка
        return reviews.Length;
    }

    private static DateTime D(string iso) => DateTime.Parse(iso, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
}

public sealed class DiscussionSeeder(MongoDbContext context) : IDataSeeder
{
    public string Collection => MongoDbContext.DiscussionsCollection;

    public async Task<int> SeedAsync(CancellationToken ct = default)
    {
        if (await context.Discussions.EstimatedDocumentCountAsync(cancellationToken: ct) > 0) return 0;

        var olena = new Author(1, "Олена Коваль");
        var andrii = new Author(2, "Андрій Мельник");
        var iryna = new Author(3, "Ірина Бондар");

        var dovbush = Discussion.Restore(ReviewSeeder.DiscussionDovbush, D("2026-11-20T09:00:00Z"), 1, "Довбуш",
            "Наскільки фільм відповідає історії?", olena, ["історія", "карпати"]);
        dovbush.PostMessage(olena, "Хтось читав історичні джерела про Довбуша?", D("2026-11-20T09:00:00Z"));
        dovbush.PostMessage(andrii, "Костюми зроблені дуже ретельно.", D("2026-11-20T10:05:00Z"));
        dovbush.PostMessage(iryna, "А мені бракувало більше про побут.", D("2026-11-20T11:30:00Z"));
        dovbush.PostMessage(olena, "Погоджуюсь, але для кіно — норм.", D("2026-11-21T08:00:00Z"));

        var dune = Discussion.Restore(ObjectId.Parse("6750a0000000000000000002"), D("2026-11-22T19:00:00Z"), 3,
            "Дюна: Частина друга", "IMAX чи звичайний зал?", andrii, ["imax", "звук"]);
        dune.PostMessage(andrii, "Хто дивився в IMAX — є різниця?", D("2026-11-22T19:00:00Z"));

        Discussion[] discussions = [dovbush, dune];
        await context.Discussions.InsertManyAsync(discussions, cancellationToken: ct);
        return discussions.Length;
    }

    private static DateTime D(string iso) => DateTime.Parse(iso, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
}

/// <summary>
/// Створює індекси при старті (до сідінгу). Визначення — у коді; повторне створення наявного індексу
/// безпечне (MongoDB його не дублює). Імена й параметри збігаються з db/p3/collections.js (Лаб. 01).
/// </summary>
public sealed class IndexCreationService(MongoDbContext context, IOptions<MongoDbOptions> options,
                                         ILogger<IndexCreationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        if (!options.Value.CreateIndexesOnStartup) return;
        var names = await CreateIndexesAsync(ct);
        logger.LogInformation("MongoDB indexes ensured: {IndexNames}", names);
    }

    public async Task<IReadOnlyList<string>> CreateIndexesAsync(CancellationToken ct = default)
    {
        var rk = Builders<Review>.IndexKeys;
        var reviewIndexes = new[]
        {
            // складений: відгуки фільму, новіші першими, без видалених
            new CreateIndexModel<Review>(rk.Ascending(r => r.MovieId).Descending(r => r.CreatedAt),
                new CreateIndexOptions<Review>
                {
                    Name = "ix_reviews_movie_createdAt",
                    PartialFilterExpression = Builders<Review>.Filter.Eq(r => r.IsDeleted, false)
                }),
            // складений: «мої відгуки»
            new CreateIndexModel<Review>(rk.Ascending("author.customerId").Descending(r => r.CreatedAt),
                new CreateIndexOptions { Name = "ix_reviews_author" }),
            // unique: один відгук глядача на фільм
            new CreateIndexModel<Review>(rk.Ascending(r => r.MovieId).Ascending("author.customerId"),
                new CreateIndexOptions<Review>
                {
                    Name = "ux_reviews_movie_author_viewer",
                    Unique = true,
                    PartialFilterExpression = new BsonDocument("type", "viewer")
                }),
            // text: повнотекстовий пошук
            new CreateIndexModel<Review>(rk.Text(r => r.Text),
                new CreateIndexOptions { Name = "tx_reviews_text", DefaultLanguage = "none" }),
            // MovieRenamed: updateMany за movieId
            new CreateIndexModel<Review>(rk.Ascending(r => r.MovieId), new CreateIndexOptions { Name = "ix_reviews_movie" }),
        };

        var dk = Builders<Discussion>.IndexKeys;
        var discussionIndexes = new[]
        {
            new CreateIndexModel<Discussion>(dk.Ascending(d => d.MovieId).Descending(d => d.LastMessageAt),
                new CreateIndexOptions { Name = "ix_discussions_movie_last" }),
            new CreateIndexModel<Discussion>(dk.Ascending("tags"), new CreateIndexOptions { Name = "ix_discussions_tags" }),
            // unique: назва теми в межах фільму
            new CreateIndexModel<Discussion>(dk.Ascending(d => d.MovieId).Ascending(d => d.Title),
                new CreateIndexOptions { Name = "ux_discussions_movie_title", Unique = true }),
        };

        var created = new List<string>();
        created.AddRange(await context.Reviews.Indexes.CreateManyAsync(reviewIndexes, ct));
        created.AddRange(await context.Discussions.Indexes.CreateManyAsync(discussionIndexes, ct));
        return created;
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

/// <summary>Запускає всі IDataSeeder після створення індексів (вимикається MongoDb:SeedOnStartup=false).</summary>
public sealed class DataSeedingHostedService(IServiceProvider services, IOptions<MongoDbOptions> options,
                                             ILogger<DataSeedingHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        if (!options.Value.SeedOnStartup) return;
        await using var scope = services.CreateAsyncScope();
        foreach (var seeder in scope.ServiceProvider.GetServices<IDataSeeder>())
        {
            var inserted = await seeder.SeedAsync(ct);
            if (inserted > 0) logger.LogInformation("Seeded {Collection}: {Inserted} documents", seeder.Collection, inserted);
            else logger.LogInformation("Seed skipped for {Collection}: collection already contains data", seeder.Collection);
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
