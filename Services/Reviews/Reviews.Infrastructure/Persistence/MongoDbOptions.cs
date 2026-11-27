namespace Reviews.Infrastructure.Persistence;

/// <summary>Секція "MongoDb" в appsettings.{Environment}.json. Пул і таймаути — параметрами рядка підключення.</summary>
public sealed class MongoDbOptions
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; set; } = "";
    public string DatabaseName { get; set; } = "";
    /// <summary>Сідінг при старті (вимикається в Production).</summary>
    public bool SeedOnStartup { get; set; } = true;
    /// <summary>Створення індексів при старті.</summary>
    public bool CreateIndexesOnStartup { get; set; } = true;
}
