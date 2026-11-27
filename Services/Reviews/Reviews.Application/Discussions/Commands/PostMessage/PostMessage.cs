using AutoMapper;
using FluentValidation;
using MediatR;
using MongoDB.Bson;
using Reviews.Application.Common.Messaging;
using Reviews.Application.Common.Models;
using Reviews.Application.Common.Validation;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Discussions.Commands.PostMessage;

/// <summary>Нове повідомлення в темі: у документі теми лишаються 3 останні, лічильник зростає.</summary>
public sealed record PostMessageCommand(string DiscussionId, long CustomerId, string AuthorName, string Text)
    : ICommand<DiscussionDto>;

public sealed class PostMessageCommandValidator : AbstractValidator<PostMessageCommand>
{
    public PostMessageCommandValidator()
    {
        RuleFor(x => x.DiscussionId).MustBeObjectId();
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.AuthorName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Text).NotEmpty().MaximumLength(4000);
    }
}

public sealed class PostMessageCommandHandler(IDiscussionRepository discussions, IMapper mapper, TimeProvider clock)
    : IRequestHandler<PostMessageCommand, DiscussionDto>
{
    public async Task<DiscussionDto> Handle(PostMessageCommand cmd, CancellationToken ct)
    {
        var discussion = await discussions.GetByIdAsync(ObjectId.Parse(cmd.DiscussionId), ct)
                         ?? throw new NotFoundException("Discussion", cmd.DiscussionId);
        var expected = discussion.Version;
        discussion.PostMessage(new Author(cmd.CustomerId, cmd.AuthorName), cmd.Text, clock.GetUtcNow().UtcDateTime);
        await discussions.UpdateAsync(discussion, expected, ct);
        return mapper.Map<DiscussionDto>(discussion);
    }
}
