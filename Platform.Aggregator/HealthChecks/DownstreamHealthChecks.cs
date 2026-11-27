using Grpc.Core;
using Grpc.Health.V1;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Platform.Aggregator.Clients;

namespace Platform.Aggregator.HealthChecks;

/// <summary>
/// Стан downstream-сервісу за стандартним gRPC Health Checking Protocol (grpc.health.v1.Health/Check):
/// короткий дедлайн 2 с, жодних побічних ефектів у сервісі. SERVING → Healthy; інший статус або
/// RpcException → failureStatus реєстрації (Unhealthy для критичних, Degraded для некритичних).
/// </summary>
public sealed class GrpcServiceHealthCheck(GrpcClientFactory clients, string clientName, string service,
                                           ILogger<GrpcServiceHealthCheck> logger) : IHealthCheck
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(2);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var client = clients.CreateClient<Health.HealthClient>(clientName);
        try
        {
            var reply = await client.CheckAsync(new HealthCheckRequest(), deadline: DateTime.UtcNow.Add(Deadline), cancellationToken: ct);
            return reply.Status == HealthCheckResponse.Types.ServingStatus.Serving
                ? HealthCheckResult.Healthy($"{service} is serving")
                : new HealthCheckResult(context.Registration.FailureStatus, $"{service} status: {reply.Status}");
        }
        catch (RpcException ex)
        {
            logger.LogWarning("Health check of {Service} failed: {GrpcStatus} {Detail}", service, ex.StatusCode, ex.Status.Detail);
            return new HealthCheckResult(context.Registration.FailureStatus, $"{service} is unavailable ({ex.StatusCode})", ex);
        }
    }
}

public static class DownstreamHealthChecksExtensions
{
    private static readonly string[] DownstreamTags = ["grpc", "downstream", Extensions.ReadyTag];

    /// <summary>
    /// Readiness Aggregator: bookings-api і catalog-api — критичні (Unhealthy), reviews-api — некритичний (Degraded),
    /// Redis-кеш — необов'язковий (Degraded). Health-клієнти без resilience-обробника: повтори зробили б
    /// перевірку повільною, а нам потрібна швидка відповідь «так / ні».
    /// </summary>
    public static IHostApplicationBuilder AddDownstreamHealthChecks(this IHostApplicationBuilder builder)
    {
        var checks = builder.Services.AddHealthChecks();
        foreach (var (service, failure) in new[]
                 {
                     (BookingsClient.ServiceName, HealthStatus.Unhealthy),
                     (CatalogClient.ServiceName, HealthStatus.Unhealthy),
                     (ReviewsClient.ServiceName, HealthStatus.Degraded)
                 })
        {
            var clientName = $"{service}-health";
            builder.Services.AddGrpcClient<Health.HealthClient>(clientName, o => o.Address = new Uri($"http://_grpc.{service}"))
                .RemoveAllResilienceHandlers();
            checks.Add(new HealthCheckRegistration(service,
                sp => new GrpcServiceHealthCheck(sp.GetRequiredService<GrpcClientFactory>(), clientName, service,
                                                 sp.GetRequiredService<ILogger<GrpcServiceHealthCheck>>()),
                failure, DownstreamTags, TimeSpan.FromSeconds(3)));
        }

        var redis = builder.Configuration.GetConnectionString("redis");
        if (!string.IsNullOrWhiteSpace(redis))
            checks.AddRedis(redis, name: "redis-cache", failureStatus: HealthStatus.Degraded,
                            tags: ["cache", "redis", Extensions.ReadyTag], timeout: TimeSpan.FromSeconds(3));
        return builder;
    }
}
