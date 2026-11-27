using MongoDB.Bson;

namespace Reviews.Domain.Common;

/// <summary>
/// Базова сутність для MongoDB: Id (ObjectId), час створення/зміни і Version для оптимістичної
/// конкурентності (оновлення застосовується лише до документа з очікуваною версією).
/// Домен знає лише тип ObjectId (MongoDB.Bson) і нічого не знає про драйвер чи колекції.
/// </summary>
public abstract class BaseEntity
{
    protected BaseEntity()
    {
        Id = ObjectId.GenerateNewId();
        CreatedAt = DateTime.UtcNow;
        Version = 1;
    }

    /// <summary>Для відновлення збережених даних (сідінг/імпорт) з фіксованим Id і датою.</summary>
    protected BaseEntity(ObjectId id, DateTime createdAt)
    {
        Id = id;
        CreatedAt = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc);
        Version = 1;
    }

    public ObjectId Id { get; protected set; }
    public DateTime CreatedAt { get; protected set; }
    public DateTime? UpdatedAt { get; protected set; }
    public int Version { get; protected set; }

    protected void Touch() => UpdatedAt = DateTime.UtcNow;

    /// <summary>Викликає репозиторій під час успішного збереження зміни.</summary>
    public void BumpVersion() => Version++;
}
