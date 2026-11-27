using Reviews.Domain.Exceptions;

namespace Reviews.Domain.Entities;

/// <summary>
/// Коментар до відгуку — вкладений документ масиву comments (без власного _id, як у Лаб. 01).
/// Створюється лише через Review.AddComment.
/// </summary>
public sealed class Comment
{
    public const int MaxLength = 1000;

    internal Comment(string authorName, string text, DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(authorName)) throw new DomainException("Comment author is required");
        if (string.IsNullOrWhiteSpace(text)) throw new DomainException("Comment text is required");
        if (text.Length > MaxLength) throw new DomainException($"Comment must be at most {MaxLength} characters");
        AuthorName = authorName.Trim();
        Text = text.Trim();
        CreatedAt = createdAt;
    }

    public string AuthorName { get; }
    public string Text { get; }
    public DateTime CreatedAt { get; }

    /// <summary>Для відновлення з БД (BsonClassMap.MapCreator).</summary>
    public static Comment Restore(string authorName, string text, DateTime createdAt) => new(authorName, text, createdAt);
}
