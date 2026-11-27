using AutoMapper;
using FluentValidation;
using MediatR;
using Reviews.Application.Common.Messaging;
using Reviews.Application.Common.Models;
using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Discussions.Commands.CreateDiscussion;

public sealed record CreateDiscussionCommand(
    long MovieId, string MovieTitle, string Title, long CustomerId, string AuthorName, IReadOnlyList<string>? Tags)
    : ICommand<DiscussionDto>;

public sealed class CreateDiscussionCommandValidator : AbstractValidator<CreateDiscussionCommand>
{
    public CreateDiscussionCommandValidator(IDiscussionRepository discussions)
    {
        RuleFor(x => x.MovieId).GreaterThan(0);
        RuleFor(x => x.MovieTitle).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Title).NotEmpty().Length(3, 200);
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.AuthorName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Tags).Must(t => t is null || t.Count <= 10).WithMessage("At most 10 tags");
        RuleForEach(x => x.Tags).NotEmpty().MaximumLength(30);
        // асинхронне правило з запитом до MongoDB: тема з такою назвою для фільму вже є
        RuleFor(x => x.Title)
            .MustAsync(async (cmd, title, ct) => !await discussions.ExistsWithTitleAsync(cmd.MovieId, title.Trim(), ct))
            .When(x => x.MovieId > 0 && !string.IsNullOrWhiteSpace(x.Title))
            .WithMessage("A discussion with this title already exists for the movie");
    }
}

public sealed class CreateDiscussionCommandHandler(IDiscussionRepository discussions, IMapper mapper)
    : IRequestHandler<CreateDiscussionCommand, DiscussionDto>
{
    public async Task<DiscussionDto> Handle(CreateDiscussionCommand cmd, CancellationToken ct)
    {
        var discussion = new Discussion(cmd.MovieId, cmd.MovieTitle, cmd.Title,
                                        new Author(cmd.CustomerId, cmd.AuthorName), cmd.Tags);
        await discussions.AddAsync(discussion, ct);
        return mapper.Map<DiscussionDto>(discussion);
    }
}
