using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Reviews.Domain.Entities;

namespace Reviews.Infrastructure.Persistence;

/// <summary>
/// Доступ до БД cinema_reviews: IMongoClient (singleton, потокобезпечний, має пул з'єднань) за налаштуваннями
/// з IOptions&lt;MongoDbOptions&gt; і типізовані колекції для доменних сутностей.
/// </summary>
public sealed class MongoDbContext
{
    public const string ReviewsCollection = "reviews";
    public const string DiscussionsCollection = "discussions";

    public MongoDbContext(IOptions<MongoDbOptions> options)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
            throw new InvalidOperationException("MongoDb:ConnectionString is not configured");
        if (string.IsNullOrWhiteSpace(settings.DatabaseName))
            throw new InvalidOperationException("MongoDb:DatabaseName is not configured");

        MongoMappings.Register();                                   // гарантовано до першого звернення
        Client = new MongoClient(settings.ConnectionString);
        Database = Client.GetDatabase(settings.DatabaseName);
    }

    public IMongoClient Client { get; }
    public IMongoDatabase Database { get; }

    public IMongoCollection<Review> Reviews => Database.GetCollection<Review>(ReviewsCollection);
    public IMongoCollection<Discussion> Discussions => Database.GetCollection<Discussion>(DiscussionsCollection);
}
