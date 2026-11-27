using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Reviews.Application.Common.Interfaces;
using Reviews.Domain.Interfaces;
using Reviews.Infrastructure.Persistence;
using Reviews.Infrastructure.Seeding;

namespace Reviews.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// MongoDB: BSON-мапінг (один раз), MongoDbOptions із секції "MongoDb" (Aspire може перевизначити
    /// рядок підключення через ConnectionStrings:reviewsdb), контекст-singleton, репозиторії, запити читання,
    /// сідери й hosted-сервіси створення індексів і сідінгу (у такому порядку).
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        MongoMappings.Register();

        services.AddOptions<MongoDbOptions>()
            .Bind(config.GetSection(MongoDbOptions.SectionName))
            .PostConfigure(o =>
            {
                var fromAspire = config.GetConnectionString("reviewsdb");
                if (!string.IsNullOrWhiteSpace(fromAspire)) o.ConnectionString = WithPoolAndTimeouts(fromAspire);
            })
            .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString) && !string.IsNullOrWhiteSpace(o.DatabaseName),
                      "MongoDb:ConnectionString and MongoDb:DatabaseName are required")
            .ValidateOnStart();

        services.AddSingleton<MongoDbContext>();
        services.AddSingleton<IMongoClient>(sp => sp.GetRequiredService<MongoDbContext>().Client);

        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IDiscussionRepository, DiscussionRepository>();
        services.AddScoped<IReviewQueries, ReviewQueries>();
        services.AddScoped<IDiscussionQueries, DiscussionQueries>();

        services.AddScoped<IDataSeeder, ReviewSeeder>();
        services.AddScoped<IDataSeeder, DiscussionSeeder>();
        services.AddSingleton<IndexCreationService>();
        services.AddHostedService(sp => sp.GetRequiredService<IndexCreationService>());   // 1) індекси
        services.AddHostedService<DataSeedingHostedService>();                             // 2) сідінг
        return services;
    }

    /// <summary>Пул і таймаути, якщо їх немає в рядку від Aspire: недоступний сервер → 503 за 5 с, а не за 30.</summary>
    private static string WithPoolAndTimeouts(string connectionString)
    {
        // параметри дописуються до рядка як є (без розбору: облікові дані Aspire не перекодовуються)
        var result = connectionString;
        foreach (var (key, value) in new[] { ("serverSelectionTimeoutMS", "5000"), ("connectTimeoutMS", "5000"), ("maxPoolSize", "50") })
        {
            if (result.Contains(key + "=", StringComparison.OrdinalIgnoreCase)) continue;
            result += (result.Contains('?') ? "&" : "?") + key + "=" + value;
        }
        return result;
    }
}
