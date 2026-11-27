using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Reviews.Application.Common.Models;
using Reviews.Domain.Entities;
using Reviews.Domain.ValueObjects;
using Reviews.Infrastructure.Persistence;

namespace Reviews.Tests;

/// <summary>BsonClassMap без бази: документ має структуру Лаб. 01 і без витоку внутрішніх полів .NET.</summary>
public class BsonMappingTests
{
    public BsonMappingTests() => MongoMappings.Register();

    private static Review Sample()
    {
        var review = new Review(ReviewType.Critic, 3, "Дюна", new Author(2, "Андрій"), new Rating(5), "Звук!",
                                new CriticInfo("Кіноблог", 92));
        review.AddComment("Олена", "Згодна", new DateTime(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc));
        return review;
    }

    [Fact]
    public void Review_document_is_camelCase_with_embedded_author_and_comments()
    {
        var doc = Sample().ToBsonDocument();

        Assert.Equal("critic", doc["type"].AsString);
        Assert.Equal(3L, doc["movieId"].AsInt64);
        Assert.Equal(5, doc["rating"].AsInt32);                                   // VO Rating → число
        Assert.Equal(2L, doc["author"]["customerId"].AsInt64);
        Assert.Equal("Кіноблог", doc["criticInfo"]["outlet"].AsString);
        Assert.Equal("Олена", doc["comments"][0]["authorName"].AsString);
        Assert.Equal(1, doc["version"].AsInt32);
        Assert.False(doc["isDeleted"].AsBoolean);
        Assert.IsType<BsonObjectId>(doc["_id"]);

        var names = doc.Names.ToList();
        Assert.DoesNotContain(names, n => n.StartsWith('_') && n != "_id");     // без _comments
        Assert.DoesNotContain(names, n => char.IsUpper(n[0]));
        Assert.DoesNotContain("discussionId", names);                           // null не пишеться
    }

    [Fact]
    public void Review_roundtrips_through_private_field_and_constructors()
    {
        var original = Sample();
        var restored = BsonSerializer.Deserialize<Review>(original.ToBsonDocument());

        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.Author, restored.Author);
        Assert.Equal(original.Rating, restored.Rating);
        Assert.Equal(original.CriticInfo, restored.CriticInfo);
        Assert.Single(restored.Comments);
    }

    [Fact]
    public void Projection_document_deserializes_straight_into_dto()
    {
        var id = ObjectId.GenerateNewId();
        var row = new BsonDocument
        {
            { "_id", id }, { "type", "viewer" }, { "movieId", 1L }, { "movieTitle", "Довбуш" },
            { "author", new BsonDocument { { "customerId", 1L }, { "displayName", "Олена" } } },
            { "rating", 5 }, { "text", "Супер" }, { "commentsCount", 2 },
            { "createdAt", DateTime.UtcNow }, { "version", 3 }
        };

        var dto = BsonSerializer.Deserialize<ReviewListItemDto>(row);

        Assert.Equal(id.ToString(), dto.Id);                                     // ObjectId → string
        Assert.Equal("Олена", dto.Author.DisplayName);
        Assert.Equal(2, dto.CommentsCount);
        Assert.Equal(3, dto.Version);
    }
}

