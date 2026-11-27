using System.ComponentModel.DataAnnotations;

namespace Reviews.WebApi.Contracts;

// Тіла HTTP-запитів. Мапляться в команди AutoMapper-профілем RequestProfile (Id — з маршруту, версія — з If-Match).
// Бізнес-валідація — у валідаторах команд (ValidationBehavior), тут лише форма запиту.

public sealed class CreateReviewRequest
{
    [Required] public string Type { get; set; } = "viewer";
    public long MovieId { get; set; }
    [Required] public string MovieTitle { get; set; } = "";
    public long CustomerId { get; set; }
    [Required] public string AuthorName { get; set; } = "";
    public int Rating { get; set; }
    public string? Text { get; set; }
    public string? CriticOutlet { get; set; }
    public int? CriticScore100 { get; set; }
    public bool? VerifiedViewer { get; set; }
    public bool? ContainsSpoilers { get; set; }
}

public sealed class UpdateReviewRequest
{
    public int Rating { get; set; }
    public string? Text { get; set; }
    public bool? ContainsSpoilers { get; set; }
}

public sealed class AddCommentRequest
{
    [Required] public string AuthorName { get; set; } = "";
    [Required] public string Text { get; set; } = "";
}

public sealed class CreateDiscussionRequest
{
    public long MovieId { get; set; }
    [Required] public string MovieTitle { get; set; } = "";
    [Required] public string Title { get; set; } = "";
    public long CustomerId { get; set; }
    [Required] public string AuthorName { get; set; } = "";
    public List<string>? Tags { get; set; }
}

public sealed class PostMessageRequest
{
    public long CustomerId { get; set; }
    [Required] public string AuthorName { get; set; } = "";
    [Required] public string Text { get; set; } = "";
}
