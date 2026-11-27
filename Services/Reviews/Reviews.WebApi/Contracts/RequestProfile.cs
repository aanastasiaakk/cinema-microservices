using AutoMapper;
using Reviews.Application.Discussions.Commands.CreateDiscussion;
using Reviews.Application.Reviews.Commands.CreateReview;
using Reviews.Application.Reviews.Commands.UpdateReview;

namespace Reviews.WebApi.Contracts;

/// <summary>HTTP-контракти → команди MediatR (тип відгуку нормалізується до нижнього регістру).</summary>
public sealed class RequestProfile : Profile
{
    public RequestProfile()
    {
        CreateMap<CreateReviewRequest, CreateReviewCommand>()
            .ForCtorParam(nameof(CreateReviewCommand.Type), o => o.MapFrom(r => (r.Type ?? "").Trim().ToLowerInvariant()));
        CreateMap<UpdateReviewRequest, UpdateReviewCommand>()
            .ForCtorParam(nameof(UpdateReviewCommand.Id), o => o.MapFrom((_, ctx) => (string)ctx.Items["id"]))
            .ForCtorParam(nameof(UpdateReviewCommand.ExpectedVersion), o => o.MapFrom((_, ctx) => (int?)ctx.Items["version"]));
        CreateMap<CreateDiscussionRequest, CreateDiscussionCommand>();
    }
}
