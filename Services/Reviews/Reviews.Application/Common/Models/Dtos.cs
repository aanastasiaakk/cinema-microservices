namespace Reviews.Application.Common.Models;

// DTO без логіки. Id — рядок ObjectId (а не сирий BSON).

public sealed record AuthorDto(long CustomerId, string DisplayName);

public sealed record CommentDto(string AuthorName, string Text, DateTime CreatedAt);

public sealed record CriticInfoDto(string Outlet, int? Score100);

/// <summary>Повний відгук (після команд і для GET за Id).</summary>
public sealed record ReviewDto(
    string Id, string Type, long MovieId, string MovieTitle, AuthorDto Author, int Rating, string? Text,
    CriticInfoDto? CriticInfo, bool? VerifiedViewer, bool? ContainsSpoilers, string? DiscussionId,
    IReadOnlyList<CommentDto> Comments, int CommentsCount, DateTime CreatedAt, DateTime? UpdatedAt, int Version);

/// <summary>Рядок списку — проєкція в MongoDB лише потрібних полів (без масиву коментарів).</summary>
public sealed record ReviewListItemDto(
    string Id, string Type, long MovieId, string MovieTitle, AuthorDto Author, int Rating, string? Text,
    int CommentsCount, DateTime CreatedAt, int Version);

/// <summary>Зведений рейтинг фільму (aggregation pipeline $match → $group).</summary>
public sealed record MovieRatingDto(long MovieId, string MovieTitle, double Average, int Count,
                                    IReadOnlyDictionary<string, int> Distribution);

public sealed record RecentMessageDto(string MessageId, string AuthorName, string Text, DateTime CreatedAt);

public sealed record DiscussionDto(
    string Id, long MovieId, string MovieTitle, string Title, AuthorDto CreatedBy, string Status,
    IReadOnlyList<string> Tags, IReadOnlyList<RecentMessageDto> RecentMessages, int MessagesCount,
    DateTime? LastMessageAt, DateTime CreatedAt, int Version);
