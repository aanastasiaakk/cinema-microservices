using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Driver;
using Hosting = Microsoft.Extensions.Hosting.Extensions;

namespace Reviews.WebApi.HealthChecks;

/// <summary>
/// reviews-db — MongoDB ping через зареєстрований IMongoClient (теги database/mongodb/ready, 5 с, Unhealthy)
/// + стандартний gRPC Health Checking Protocol для Aggregator.
/// </summary>
public static class ReviewsHealthChecks
{
    public static IServiceCollection AddReviewsHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddMongoDb(sp => sp.GetRequiredService<IMongoClient>(),
                name: "reviews-db",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["database", "mongodb", Hosting.ReadyTag],
                timeout: TimeSpan.FromSeconds(5));
        services.AddGrpcHealthChecks(o => o.Services.Map("", r => r.Tags.Contains(Hosting.ReadyTag)));
        return services;
    }
}
