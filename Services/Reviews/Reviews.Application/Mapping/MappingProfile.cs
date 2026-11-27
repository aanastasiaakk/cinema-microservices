using AutoMapper;
using MongoDB.Bson;
using Reviews.Application.Common.Models;
using Reviews.Domain.Entities;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Mapping;

/// <summary>
/// Entity → DTO: ObjectId → string, Value Objects (Author, Rating, CriticInfo) → прості DTO/числа,
/// вкладені масиви (comments, recentMessages, tags) → списки DTO, enum → рядок як у документі.
/// </summary>
public sealed class ReviewProfile : Profile
{
    public ReviewProfile()
    {
        CreateMap<ObjectId, string>().ConvertUsing(id => id.ToString());
        CreateMap<ObjectId?, string?>().ConvertUsing(id => id.HasValue ? id.Value.ToString() : null);
        CreateMap<Rating, int>().ConvertUsing(r => r.Value);
        CreateMap<ReviewType, string>().ConvertUsing(t => t.ToString().ToLowerInvariant());
        CreateMap<DiscussionStatus, string>().ConvertUsing(s => s.ToString().ToLowerInvariant());

        CreateMap<Author, AuthorDto>();
        CreateMap<CriticInfo, CriticInfoDto>();
        CreateMap<Comment, CommentDto>();
        CreateMap<RecentMessage, RecentMessageDto>();

        CreateMap<Review, ReviewDto>()
            .ForCtorParam(nameof(ReviewDto.CommentsCount), o => o.MapFrom(r => r.Comments.Count));

        CreateMap<Discussion, DiscussionDto>();
    }
}
