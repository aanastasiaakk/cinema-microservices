using FluentValidation;
using MediatR;
using MongoDB.Bson;
using Reviews.Application.Common.Messaging;
using Reviews.Application.Common.Validation;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;

namespace Reviews.Application.Discussions.Commands.CloseDiscussion;

public sealed record CloseDiscussionCommand(string Id, int? ExpectedVersion) : ICommand;

public sealed class CloseDiscussionCommandValidator : AbstractValidator<CloseDiscussionCommand>
{
    public CloseDiscussionCommandValidator() => RuleFor(x => x.Id).MustBeObjectId();
}

public sealed class CloseDiscussionCommandHandler(IDiscussionRepository discussions) : IRequestHandler<CloseDiscussionCommand>
{
    public async Task Handle(CloseDiscussionCommand cmd, CancellationToken ct)
    {
        var discussion = await discussions.GetByIdAsync(ObjectId.Parse(cmd.Id), ct)
                         ?? throw new NotFoundException("Discussion", cmd.Id);
        var expected = cmd.ExpectedVersion ?? discussion.Version;
        if (expected != discussion.Version)
            throw new ConflictException($"Discussion {cmd.Id} has version {discussion.Version}, but {expected} was expected");
        discussion.Close();
        await discussions.UpdateAsync(discussion, expected, ct);
    }
}
