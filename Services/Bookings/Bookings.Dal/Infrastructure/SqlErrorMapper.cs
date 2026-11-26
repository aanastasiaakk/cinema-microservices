using Bookings.Domain.Exceptions;
using Microsoft.Data.SqlClient;

namespace Bookings.Dal.Infrastructure;

/// <summary>
/// Перетворює SqlException на доменні винятки. Сирі SQL-повідомлення далі DAL не йдуть.
/// Номери 500xx — керовані помилки (THROW) з процедур Лаб. 01.
/// </summary>
public static class SqlErrorMapper
{
    private static readonly HashSet<int> NotFound = [50001, 50012, 50031];
    private static readonly HashSet<int> Conflict =
        [50002, 50010, 50011, 50020, 50030, 50032, 50033, 50040, 50041, 50042];

    public static Exception Map(SqlException ex)
    {
        // процедура може повернути кілька помилок (наприклад, THROW + 266) — шукаємо «нашу»
        foreach (SqlError error in ex.Errors)
        {
            if (NotFound.Contains(error.Number))
                return new NotFoundException(error.Message, ex);
            if (Conflict.Contains(error.Number))
                return new BusinessConflictException(error.Message, ex);
            if (error.Number is 2601 or 2627)              // порушення UNIQUE
                return new BusinessConflictException("Record with the same key already exists", ex);
            if (error.Number == 547)                       // порушення FK / CHECK
                return new BusinessConflictException("Operation violates a data integrity constraint", ex);
        }
        return ex;                                         // невідома помилка -> 500 (деталі лише в лог)
    }

    /// <summary>Виконує дію і перекладає SqlException у доменний виняток.</summary>
    public static async Task<T> RunAsync<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        catch (SqlException ex) when (Map(ex) is var mapped && !ReferenceEquals(mapped, ex)) { throw mapped; }
    }

    public static async Task RunAsync(Func<Task> action)
    {
        try { await action(); }
        catch (SqlException ex) when (Map(ex) is var mapped && !ReferenceEquals(mapped, ex)) { throw mapped; }
    }
}
