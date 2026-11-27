namespace Reviews.Domain.Entities;

/// <summary>Тип відгуку (у документі — рядок: "viewer" | "critic" | "quick").</summary>
public enum ReviewType
{
    Viewer,
    Critic,
    Quick
}

/// <summary>Статус обговорення (у документі — "open" | "closed" | "archived").</summary>
public enum DiscussionStatus
{
    Open,
    Closed,
    Archived
}
