using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Bookings.Dal.Infrastructure;

/// <summary>
/// Ініціалізація ПОРОЖНЬОЇ БД у контейнері Aspire скриптами Лаб. 01 (db/p1: schema → procedures → seed).
/// Скрипти ідемпотентні, тож повторний запуск безпечний. Пакети розділяються рядками GO
/// (GO — команда sqlcmd/SSMS, а не T-SQL). Вмикається налаштуванням Database:ApplyScripts.
/// </summary>
public static partial class DatabaseInitializer
{
    private static readonly string[] Scripts = ["schema.sql", "procedures.sql", "seed.sql"];

    [GeneratedRegex(@"^\s*GO\s*(?:--.*)?$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoSeparator();

    public static async Task ApplyAsync(string connectionString, string scriptsFolder,
                                        ILogger logger, CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await OpenWithRetryAsync(connection, logger, ct);

        foreach (var file in Scripts)
        {
            var path = Path.Combine(scriptsFolder, file);
            if (!File.Exists(path))
                throw new FileNotFoundException($"SQL script not found: {path}");

            var batches = GoSeparator().Split(await File.ReadAllTextAsync(path, ct))
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .ToList();

            foreach (var batch in batches)
            {
                // Текст скрипта — файл з репозиторію (довірене джерело), не вхідні дані користувача
                await using var command = new SqlCommand(batch, connection) { CommandTimeout = 120 };
                await command.ExecuteNonQueryAsync(ct);
            }
            logger.LogInformation("Applied {Script} ({Batches} batches)", file, batches.Count);
        }
    }

    private static async Task OpenWithRetryAsync(SqlConnection connection, ILogger logger, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await connection.OpenAsync(ct);
                return;
            }
            catch (SqlException ex) when (attempt < 10)
            {
                logger.LogWarning("Database is not ready yet (attempt {Attempt}): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }
    }
}
