using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using Reviews.Application.Common.Models;
using Reviews.Domain.Common;
using Reviews.Domain.Entities;
using Reviews.Domain.ValueObjects;

namespace Reviews.Infrastructure.Persistence;

/// <summary>
/// BSON-мапінг БЕЗ атрибутів у домені: конвенція camelCase (як документи Лаб. 01), ігнорування
/// невідомих полів (гнучка схема: photos, pros, watchedFormat…), мапінг приватних полів-колекцій,
/// створення незмінних Value Objects через конструктор (MapCreator), власні серіалізатори для
/// Rating (число) і enum-ів (рядок у нижньому регістрі). Реєструється ОДИН раз при старті.
/// </summary>
public static class MongoMappings
{
    private static readonly object Sync = new();
    private static bool _registered;

    public static void Register()
    {
        lock (Sync)
        {
            if (_registered) return;

            ConventionRegistry.Register("cinema-reviews", new ConventionPack
            {
                new CamelCaseElementNameConvention(),
                new IgnoreExtraElementsConvention(true),
                new IgnoreIfNullConvention(true)
            }, t => t.Namespace?.StartsWith("Reviews.", StringComparison.Ordinal) == true);

            // Доменні класи мапляться ЯВНО (без AutoMap): сутності мають приватні setter-и й публічні конструктори
            // з валідацією, тож AutoMap обрав би конструктор як creator. Конвенції camelCase/IgnoreIfNull
            // застосовуються до кожного змапленого члена в Domain(...) — ті самі, що зареєстровані вище.
            Domain<BaseEntity>(cm =>
            {
                cm.MapIdProperty(e => e.Id);
                cm.MapProperty(e => e.CreatedAt);
                cm.MapProperty(e => e.UpdatedAt);
                cm.MapProperty(e => e.Version);
            });

            // ---------- Value Objects: лише читання властивостей + створення через конструктор ----------
            Domain<Author>(cm =>
            {
                cm.MapProperty(a => a.CustomerId);
                cm.MapProperty(a => a.DisplayName);
                cm.MapCreator(a => new Author(a.CustomerId, a.DisplayName));
            });
            Domain<CriticInfo>(cm =>
            {
                cm.MapProperty(c => c.Outlet);
                cm.MapProperty(c => c.Score100);
                cm.MapCreator(c => new CriticInfo(c.Outlet, c.Score100));
            });
            Domain<Comment>(cm =>
            {
                cm.MapProperty(c => c.AuthorName);
                cm.MapProperty(c => c.Text);
                cm.MapProperty(c => c.CreatedAt);
                cm.MapCreator(c => Comment.Restore(c.AuthorName, c.Text, c.CreatedAt));
            });
            Domain<RecentMessage>(cm =>
            {
                cm.MapProperty(m => m.MessageId);
                cm.MapProperty(m => m.AuthorName);
                cm.MapProperty(m => m.Text);
                cm.MapProperty(m => m.CreatedAt);
                cm.MapCreator(m => RecentMessage.Restore(m.MessageId, m.AuthorName, m.Text, m.CreatedAt));
            });

            // ---------- Сутності: приватний конструктор без параметрів для десеріалізації ----------
            Domain<Review>(cm =>
            {
                cm.SetCreator(() => (Review)Activator.CreateInstance(typeof(Review), nonPublic: true)!);
                cm.MapProperty(r => r.Type).SetSerializer(new LowerCaseEnumSerializer<ReviewType>());
                cm.MapProperty(r => r.MovieId);
                cm.MapProperty(r => r.MovieTitle);
                cm.MapProperty(r => r.Author);
                cm.MapProperty(r => r.Rating).SetSerializer(new RatingSerializer());
                cm.MapProperty(r => r.Text);
                cm.MapProperty(r => r.CriticInfo);
                cm.MapProperty(r => r.VerifiedViewer);
                cm.MapProperty(r => r.ContainsSpoilers);
                cm.MapProperty(r => r.DiscussionId);
                cm.MapProperty(r => r.IsDeleted);
                cm.MapField("_comments").SetElementName("comments");     // приватне поле → масив comments
            });
            Domain<Discussion>(cm =>
            {
                cm.SetCreator(() => (Discussion)Activator.CreateInstance(typeof(Discussion), nonPublic: true)!);
                cm.MapProperty(d => d.MovieId);
                cm.MapProperty(d => d.MovieTitle);
                cm.MapProperty(d => d.Title);
                cm.MapProperty(d => d.CreatedBy);
                cm.MapProperty(d => d.Status).SetSerializer(new LowerCaseEnumSerializer<DiscussionStatus>());
                cm.MapProperty(d => d.MessagesCount);
                cm.MapProperty(d => d.LastMessageAt);
                cm.MapField("_tags").SetElementName("tags");
                cm.MapField("_recentMessages").SetElementName("recentMessages");
            });

            // ---------- DTO читання: проєкція з MongoDB одразу в DTO (ObjectId → string) ----------
            var objectIdAsString = new StringSerializer(BsonType.ObjectId);
            var nullableObjectIdAsString = new NullableObjectIdStringSerializer();
            RegisterDto<AuthorDto>();
            RegisterDto<CommentDto>();
            RegisterDto<CriticInfoDto>();
            RegisterDto<ReviewListItemDto>(cm => cm.MapIdMember(cm.GetMemberMap(nameof(ReviewListItemDto.Id)).MemberInfo).SetSerializer(objectIdAsString));
            RegisterDto<ReviewDto>(cm =>
            {
                cm.MapIdMember(cm.GetMemberMap(nameof(ReviewDto.Id)).MemberInfo).SetSerializer(objectIdAsString);
                cm.GetMemberMap(nameof(ReviewDto.DiscussionId)).SetSerializer(nullableObjectIdAsString);
            });
            RegisterDto<RecentMessageDto>(cm => cm.GetMemberMap(nameof(RecentMessageDto.MessageId)).SetSerializer(objectIdAsString));
            RegisterDto<DiscussionDto>(cm => cm.MapIdMember(cm.GetMemberMap(nameof(DiscussionDto.Id)).MemberInfo).SetSerializer(objectIdAsString));

            _registered = true;
        }
    }

    private static readonly CamelCaseElementNameConvention CamelCase = new();

    /// <summary>
    /// Явний мапінг доменного класу + конвенції для кожного члена: camelCase (якщо ім'я не задано вручну,
    /// як для приватних полів _comments → comments), null-значення не записуються (гнучка схема),
    /// невідомі елементи документа ігноруються.
    /// </summary>
    private static void Domain<T>(Action<BsonClassMap<T>> configure)
        => BsonClassMap.RegisterClassMap<T>(cm =>
        {
            configure(cm);
            cm.SetIgnoreExtraElements(true);
            foreach (var member in cm.DeclaredMemberMaps)
            {
                if (member.ElementName == member.MemberName) CamelCase.Apply(member);
                var type = member.MemberType;
                if (!type.IsValueType || Nullable.GetUnderlyingType(type) is not null) member.SetIgnoreIfNull(true);
            }
        });

    private static void RegisterDto<T>(Action<BsonClassMap<T>>? configure = null)
        => BsonClassMap.RegisterClassMap<T>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            configure?.Invoke(cm);
        });
}

/// <summary>Rating (VO) зберігається як звичайне число "rating": 5 — структура документа з Лаб. 01.</summary>
public sealed class RatingSerializer : SerializerBase<Rating>
{
    public override Rating Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        => new(context.Reader.ReadInt32());

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Rating value)
        => context.Writer.WriteInt32(value.Value);
}

/// <summary>Enum ↔ рядок у нижньому регістрі ("viewer", "open"), як у документах Лаб. 01.</summary>
public sealed class LowerCaseEnumSerializer<TEnum> : SerializerBase<TEnum> where TEnum : struct, Enum
{
    public override TEnum Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        => Enum.Parse<TEnum>(context.Reader.ReadString(), ignoreCase: true);

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TEnum value)
        => context.Writer.WriteString(value.ToString().ToLowerInvariant());
}

/// <summary>ObjectId (або null) у БД ↔ string? у DTO.</summary>
public sealed class NullableObjectIdStringSerializer : SerializerBase<string?>
{
    public override string? Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        if (context.Reader.CurrentBsonType == BsonType.Null)
        {
            context.Reader.ReadNull();
            return null;
        }
        return context.Reader.ReadObjectId().ToString();
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, string? value)
    {
        if (value is null) context.Writer.WriteNull();
        else context.Writer.WriteObjectId(ObjectId.Parse(value));
    }
}
