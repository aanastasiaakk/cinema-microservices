using System.Diagnostics;
using MediatR;
using Reviews.Application.Reviews.Commands.AddComment;
using Reviews.Application.Reviews.Commands.CreateReview;
using Reviews.Application.Reviews.Commands.DeleteReview;
using Reviews.Application.Reviews.Commands.UpdateReview;

namespace Reviews.WebApi.Metrics;

/// <summary>
/// Найзовнішній MediatR-behavior (реєструється першим): тривалість КОЖНОЇ команди/запиту, зокрема відхилених
/// валідацією, і бізнес-лічильники одразу після успішної команди. Працює і для REST, і для gRPC.
/// </summary>
public sealed class MetricsBehavior<TRequest, TResponse>(ReviewMetrics metrics) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var operation = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();
        metrics.Enter();
        try
        {
            var response = await next(ct);
            metrics.RecordDuration(operation, success: true, sw.Elapsed.TotalMilliseconds);
            switch (request)
            {
                case CreateReviewCommand create: metrics.ReviewCreated(create.Type, create.Rating); break;
                case UpdateReviewCommand: metrics.ReviewUpdated(); break;
                case DeleteReviewCommand: metrics.ReviewDeleted(); break;
                case AddCommentCommand: metrics.CommentAdded(); break;
            }
            return response;
        }
        catch
        {
            metrics.RecordDuration(operation, success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
        finally
        {
            metrics.Exit();
        }
    }
}
