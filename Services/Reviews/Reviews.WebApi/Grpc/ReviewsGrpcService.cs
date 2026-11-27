using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MediatR;
using Reviews.Application.Reviews.Queries.GetMovieRating;
using Reviews.Application.Reviews.Queries.GetReviewsList;
using Reviews.Domain.Exceptions;
using ReviewsGrpc = Reviews.Grpc;

namespace Reviews.WebApi.Grpc;

/// <summary>
/// gRPC-адаптер Clean Architecture: як і контролери, працює ЛИШЕ через ISender (MediatR), тож проходить
/// той самий pipeline (Logging → Validation → Caching → Performance). Винятки → RpcException:
/// NotFound → NOT_FOUND, ValidationException (FluentValidation) → INVALID_ARGUMENT, DomainException → INVALID_ARGUMENT.
/// </summary>
public sealed class ReviewsGrpcService(ISender sender) : ReviewsGrpc.ReviewsService.ReviewsServiceBase
{
    private const int MaxTake = 20;

    public override Task<ReviewsGrpc.MovieRatingReply> GetMovieRating(ReviewsGrpc.GetMovieRatingRequest request, ServerCallContext context)
        => Guard(async () =>
        {
            var rating = await sender.Send(new GetMovieRatingQuery(request.MovieId), context.CancellationToken);
            var reply = new ReviewsGrpc.MovieRatingReply
            {
                MovieId = rating.MovieId,
                MovieTitle = rating.MovieTitle,
                Average = rating.Average,
                Count = rating.Count
            };
            foreach (var (stars, count) in rating.Distribution) reply.Distribution[int.Parse(stars)] = count;
            return reply;
        });

    public override Task<ReviewsGrpc.ReviewsReply> GetLatestReviews(ReviewsGrpc.GetLatestReviewsRequest request, ServerCallContext context)
        => Guard(async () =>
        {
            if (request.Take is < 1 or > MaxTake)
                throw new RpcException(new Status(StatusCode.InvalidArgument, $"take must be between 1 and {MaxTake}"));
            var page = await sender.Send(new GetReviewsListQuery(MovieId: request.MovieId, SortBy: "createdAt", SortDir: "desc",
                                                                 PageSize: request.Take), context.CancellationToken);
            var reply = new ReviewsGrpc.ReviewsReply { TotalCount = page.TotalCount };
            reply.Reviews.AddRange(page.Items.Select(r => new ReviewsGrpc.ReviewReply
            {
                Id = r.Id,
                Type = r.Type,
                AuthorName = r.Author.DisplayName,
                Rating = r.Rating,
                Text = r.Text ?? "",
                CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc))
            }));
            return reply;
        });

    private static async Task<T> Guard<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (NotFoundException ex) { throw new RpcException(new Status(StatusCode.NotFound, ex.Message)); }
        catch (FluentValidation.ValidationException ex)
        {
            var errors = string.Join("; ", ex.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
            throw new RpcException(new Status(StatusCode.InvalidArgument, errors));
        }
        catch (DomainException ex) { throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message)); }
        catch (ConflictException ex) { throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message)); }
    }
}
