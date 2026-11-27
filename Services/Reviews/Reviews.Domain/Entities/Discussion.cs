using MongoDB.Bson;
using Reviews.Domain.Common;
using Reviews.Domain.Exceptions;
using Reviews.Domain.ValueObjects;

namespace Reviews.Domain.Entities;

/// <summary>Останнє повідомлення, вбудоване в обговорення (гібридний підхід Лаб. 01: лише 3 останні).</summary>
public sealed class RecentMessage
{
    internal RecentMessage(ObjectId messageId, string authorName, string text, DateTime createdAt)
    {
        MessageId = messageId;
        AuthorName = authorName;
        Text = text;
        CreatedAt = createdAt;
    }

    public ObjectId MessageId { get; }
    public string AuthorName { get; }
    public string Text { get; }
    public DateTime CreatedAt { get; }

    public static RecentMessage Restore(ObjectId messageId, string authorName, string text, DateTime createdAt)
        => new(messageId, authorName, text, createdAt);
}

/// <summary>Тема обговорення фільму (документ discussions з Лаб. 01).</summary>
public sealed class Discussion : BaseEntity
{
    public const int RecentLimit = 3;

    // не readonly: поля заповнює драйвер MongoDB під час десеріалізації
    private List<string> _tags = [];
    private List<RecentMessage> _recentMessages = [];

    private Discussion() { }

    public Discussion(long movieId, string movieTitle, string title, Author createdBy, IEnumerable<string>? tags = null)
    {
        if (movieId <= 0) throw new DomainException("MovieId must be positive");
        if (string.IsNullOrWhiteSpace(movieTitle)) throw new DomainException("Movie title is required");
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length is < 3 or > 200)
            throw new DomainException("Title must be 3–200 characters");
        MovieId = movieId;
        MovieTitle = movieTitle.Trim();
        Title = title.Trim();
        CreatedBy = createdBy ?? throw new DomainException("Author is required");
        Status = DiscussionStatus.Open;
        foreach (var tag in tags ?? []) AddTag(tag);
    }

    public long MovieId { get; private set; }
    public string MovieTitle { get; private set; } = "";
    public string Title { get; private set; } = "";
    public Author CreatedBy { get; private set; } = null!;
    public DiscussionStatus Status { get; private set; }
    public int MessagesCount { get; private set; }
    public DateTime? LastMessageAt { get; private set; }

    public IReadOnlyCollection<string> Tags => _tags.AsReadOnly();
    public IReadOnlyCollection<RecentMessage> RecentMessages => _recentMessages.AsReadOnly();

    public static Discussion Restore(ObjectId id, DateTime createdAt, long movieId, string movieTitle, string title,
                                     Author createdBy, IEnumerable<string>? tags = null)
    {
        var discussion = new Discussion(movieId, movieTitle, title, createdBy, tags);
        discussion.Id = id;
        discussion.CreatedAt = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc);
        return discussion;
    }

    public void AddTag(string tag)
    {
        var normalized = tag.Trim().ToLowerInvariant();
        if (normalized.Length is 0 or > 30) throw new DomainException("Tag must be 1–30 characters");
        if (!_tags.Contains(normalized)) _tags.Add(normalized);
    }

    /// <summary>Нове повідомлення: лічильник, час останнього і лише 3 останні вбудовані копії.</summary>
    public RecentMessage PostMessage(Author author, string text, DateTime nowUtc)
    {
        if (Status != DiscussionStatus.Open) throw new DomainException($"Discussion is {Status.ToString().ToLowerInvariant()}");
        if (string.IsNullOrWhiteSpace(text) || text.Length > 4000) throw new DomainException("Message must be 1–4000 characters");
        var message = new RecentMessage(ObjectId.GenerateNewId(), author.DisplayName, text.Trim(), nowUtc);
        _recentMessages.Add(message);
        if (_recentMessages.Count > RecentLimit) _recentMessages.RemoveAt(0);
        MessagesCount++;
        LastMessageAt = nowUtc;
        Touch();
        return message;
    }

    public void Close()
    {
        if (Status != DiscussionStatus.Open) throw new DomainException("Only an open discussion can be closed");
        Status = DiscussionStatus.Closed;
        Touch();
    }
}
