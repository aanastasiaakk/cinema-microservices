using Microsoft.Extensions.Diagnostics.HealthChecks;
using ReadyTag = Microsoft.Extensions.Hosting.Extensions;

namespace Catalog.Api.HealthChecks;

/// <summary>
/// Health checks каталогу:
///  • catalog-db — SQL Server, легкий SELECT 1, 5 с, Unhealthy (без БД каталог не працює);
///  • redis-cache — PING Redis, 3 с, Degraded: кеш необов'язковий, сервіс читає з БД (graceful degradation);
///  • gRPC Health Checking Protocol — стан для Aggregator на основі перевірок з тегом ready.
/// </summary>
public static class CatalogHealthChecks
{
    public static IServiceCollection AddCatalogHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var checks = services.AddHealthChecks()
            .AddSqlServer(
                connectionString: new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(configuration.GetConnectionString("CinemaCatalog")!)
                    { ConnectTimeout = 3 }.ConnectionString,                       // health check — за секунди, не 15 с
                healthQuery: "SELECT 1;",
                name: "catalog-db",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["database", "sql", ReadyTag.ReadyTag],
                timeout: TimeSpan.FromSeconds(5));

        var redis = configuration.GetConnectionString("redis");
        if (!string.IsNullOrWhiteSpace(redis))
            checks.AddRedis(redis, name: "redis-cache", failureStatus: HealthStatus.Degraded,     // кеш необов'язковий
                            tags: ["cache", "redis", ReadyTag.ReadyTag], timeout: TimeSpan.FromSeconds(3));

        services.AddGrpcHealthChecks(o => o.Services.Map("", r => r.Tags.Contains(ReadyTag.ReadyTag)));
        return services;
    }
}
