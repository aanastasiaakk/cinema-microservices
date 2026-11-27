using MongoDB.Bson;
using Reviews.Domain.Common;
using Reviews.Domain.Exceptions;
using Reviews.Domain.ValueObjects;

namespace Reviews.Domain.Entities;

/// <summary>
/// Відгук — корінь агрегату (документ reviews з Лаб. 01). Вбудовані author (VO), rating (VO),
/// criticInfo (VO) і масив comments (приватне поле _comments). Стан змінюється лише методами.
/// </summary>
public sealed class Review : BaseEntity
{
    public const int MaxTextLength = 5000;
    public const int MaxComments = 50;           // обмежений вбудований масив (як у валідаторі колекції)

    // не readonly: драйвер MongoDB заповнює поле під час десеріалізації (BsonClassMap.MapField)
    private List<Comment> _comments = [];

    private Review() { }                          // для десеріалізації

    public Review(ReviewType type, long movieId, string movieTitle, Author author, Rating rating,
                  string? text, CriticInfo? criticInfo = null, bool? verifiedViewer = null, bool? containsSpoilers = null)
    {
        if (movieId <= 0) throw new DomainException("MovieId must be positive");
        if (string.IsNullOrWhiteSpace(movieTitle)) throw new DomainException("Movie title is required");
        Type = type;
        MovieId = movieId;
        MovieTitle = movieTitle.Trim();
        Author = author ?? throw new DomainException("Author is required");
        Rating = rating ?? throw new DomainException("Rating is required");
        SetText(text);
        SetCriticInfo(criticInfo);
        VerifiedViewer = type == ReviewType.Viewer ? verifiedViewer ?? false : null;
        ContainsSpoilers = containsSpoilers;
    }

    public ReviewType Type { get; private set; }
    /// <summary>Id фільму з контексту Catalog (без FK).</summary>
    public long MovieId { get; private set; }
    /// <summary>Денормалізована копія Movies.Title, оновлюється подією MovieRenamed.</summary>
    public string MovieTitle { get; private set; } = "";
    public Author Author { get; private set; } = null!;
    public Rating Rating { get; private set; } = null!;
    public string? Text { get; private set; }
    public CriticInfo? CriticInfo { get; private set; }
    public bool? VerifiedViewer { get; private set; }
    public bool? ContainsSpoilers { get; private set; }
    public ObjectId? DiscussionId { get; private set; }
    public bool IsDeleted { get; private set; }

    public IReadOnlyCollection<Comment> Comments => _comments.AsReadOnly();

    /// <summary>Відновлення документа з фіксованим Id/датою (сідінг, імпорт).</summary>
    public static Review Restore(ObjectId id, DateTime createdAt, ReviewType type, long movieId, string movieTitle,
                                 Author author, Rating rating, string? text, CriticInfo? criticInfo = null,
                                 bool? verifiedViewer = null, bool? containsSpoilers = null)
    {
        var review = new Review(type, movieId, movieTitle, author, rating, text, criticInfo, verifiedViewer, containsSpoilers);
        review.Id = id;
        review.CreatedAt = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc);
        return review;
    }

    /// <summary>Редагування оцінки й тексту автором.</summary>
    public void Edit(Rating rating, string? text, bool? containsSpoilers)
    {
        EnsureNotDeleted();
        Rating = rating ?? throw new DomainException("Rating is required");
        SetText(text);
        ContainsSpoilers = containsSpoilers;
        Touch();
    }

    public Comment AddComment(string authorName, string text, DateTime nowUtc)
    {
        EnsureNotDeleted();
        if (_comments.Count >= MaxComments)
            throw new DomainException($"A review can have at most {MaxComments} comments; continue in a discussion");
        var comment = new Comment(authorName, text, nowUtc);
        _comments.Add(comment);
        Touch();
        return comment;
    }

    public void LinkDiscussion(ObjectId discussionId)
    {
        EnsureNotDeleted();
        DiscussionId = discussionId;
        Touch();
    }

    /// <summary>М'яке видалення: документ лишається (isDeleted = true), зникає з вибірок і рейтингу.</summary>
    public void Delete()
    {
        EnsureNotDeleted();
        IsDeleted = true;
        Touch();
    }

    /// <summary>Подія MovieRenamed з Каталогу: оновлення денормалізованої назви.</summary>
    public void RenameMovie(string movieTitle)
    {
        if (string.IsNullOrWhiteSpace(movieTitle)) throw new DomainException("Movie title is required");
        MovieTitle = movieTitle.Trim();
        Touch();
    }

    private void SetText(string? text)
    {
        if (Type != ReviewType.Quick && string.IsNullOrWhiteSpace(text))
            throw new DomainException($"Text is required for a {Type.ToString().ToLowerInvariant()} review");
        if (text is { Length: > MaxTextLength })
            throw new DomainException($"Text must be at most {MaxTextLength} characters");
        Text = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private void SetCriticInfo(CriticInfo? criticInfo)
    {
        if (Type == ReviewType.Critic && criticInfo is null)
            throw new DomainException("Critic review requires criticInfo");
        if (Type != ReviewType.Critic && criticInfo is not null)
            throw new DomainException("Only critic reviews can have criticInfo");
        CriticInfo = criticInfo;
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted) throw new DomainException("Review is deleted");
    }
}
