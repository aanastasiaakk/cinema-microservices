namespace Catalog.Bll.Caching;

/// <summary>
/// Передбачувана схема ключів "catalog:{тип}:{id}" — ключ легко сформувати й інвалідувати з будь-якого
/// місця, а префікс "catalog:" ізолює дані цього сервісу в спільному Redis.
/// </summary>
public static class CacheKeys
{
    public const string Prefix = "catalog:";
    public static string Movie(long id) => $"{Prefix}movie:{id}";
    public static string Showtime(long id) => $"{Prefix}showtime:{id}";
    public const string Genres = Prefix + "genres";
    public const string Halls = Prefix + "halls";
}

/// <summary>
/// TTL за типом даних. L1 (пам'ять процесу) ЗАВЖДИ коротший за L2 (Redis): інакше застарілий запис
/// у пам'яті одного екземпляра пережив би оновлення, яке інші екземпляри вже бачать у Redis.
/// </summary>
public sealed record CachePolicy(string Name, TimeSpan L1Sliding, TimeSpan L1Absolute, TimeSpan L2Absolute)
{
    /// <summary>Картка фільму: часто читається, змінюється рідко (ціна, жанри).</summary>
    public static readonly CachePolicy Movie = new("movie", TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30));

    /// <summary>Сеанс: може бути скасований — коротші терміни.</summary>
    public static readonly CachePolicy Showtime = new("showtime", TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10));

    /// <summary>Довідники (жанри, зали): майже не змінюються — absolute expiration.</summary>
    public static readonly CachePolicy Reference = new("reference", TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));
}
