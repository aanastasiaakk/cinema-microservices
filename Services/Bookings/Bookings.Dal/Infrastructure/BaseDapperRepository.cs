using Bookings.Dal.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bookings.Dal.Infrastructure;

/// <summary>
/// Спільний CRUD на ADO.NET + Dapper, написаний ОДИН раз для всіх Dapper-репозиторіїв.
/// Конкретні репозиторії задають лише ім'я таблиці, SQL вставки/оновлення та свої специфічні запити.
///
/// Безпека: ім'я таблиці (<see cref="TableName"/>) і тексти InsertSql/UpdateSql — КОНСТАНТИ з коду
/// конкретного репозиторію, а не вхідні дані користувача, тому їхня підстановка в текст запиту безпечна.
/// Усі ЗНАЧЕННЯ передаються лише параметрами Dapper (@Id, @Skip, ...).
/// Dapper не відкриває/закриває з'єднання сам — це робить Unit of Work (EnsureOpenAsync + DisposeAsync).
/// </summary>
public abstract class BaseDapperRepository<T> : RepositoryBase, IGenericRepository<T> where T : class
{
    protected BaseDapperRepository(
        SqlConnection connection,
        Func<SqlTransaction?> currentTransaction,
        string tableName,
        bool hasSoftDelete)
        : base(connection, currentTransaction)
    {
        TableName = tableName;
        HasSoftDelete = hasSoftDelete;
    }

    protected string TableName { get; }
    protected bool HasSoftDelete { get; }

    /// <summary>INSERT ... OUTPUT INSERTED.Id VALUES (@Prop, ...) — параметри беруться з властивостей T.</summary>
    protected abstract string InsertSql { get; }

    /// <summary>UPDATE ... WHERE Id = @Id — параметри беруться з властивостей T.</summary>
    protected abstract string UpdateSql { get; }

    /// <summary>Сортування для сторінкового читання (константа).</summary>
    protected virtual string DefaultOrderBy => "Id";

    private string NotDeleted => HasSoftDelete ? " AND IsDeleted = 0" : "";

    public virtual Task<T?> GetByIdAsync(long id, CancellationToken ct = default) =>
        SqlErrorMapper.RunAsync(async () =>
        {
            await EnsureOpenAsync(ct);
            return await Connection.QuerySingleOrDefaultAsync<T>(Command(
                $"SELECT * FROM {TableName} WHERE Id = @Id{NotDeleted}", new { Id = id }, ct));
        });

    public virtual Task<IReadOnlyList<T>> GetPageAsync(int page, int pageSize, CancellationToken ct = default) =>
        SqlErrorMapper.RunAsync<IReadOnlyList<T>>(async () =>
        {
            await EnsureOpenAsync(ct);
            var where = HasSoftDelete ? "WHERE IsDeleted = 0" : "";
            var rows = await Connection.QueryAsync<T>(Command(
                $"SELECT * FROM {TableName} {where} ORDER BY {DefaultOrderBy} " +
                "OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY",
                new { Skip = (page - 1) * pageSize, Take = pageSize }, ct));
            return rows.AsList();
        });

    public virtual Task<long> AddAsync(T entity, CancellationToken ct = default) =>
        SqlErrorMapper.RunAsync(async () =>
        {
            await EnsureOpenAsync(ct);
            return await Connection.ExecuteScalarAsync<long>(Command(InsertSql, entity, ct));
        });

    public virtual Task<bool> UpdateAsync(T entity, CancellationToken ct = default) =>
        SqlErrorMapper.RunAsync(async () =>
        {
            await EnsureOpenAsync(ct);
            return await Connection.ExecuteAsync(Command(UpdateSql, entity, ct)) == 1;
        });

    /// <summary>Для таблиць з IsDeleted — м'яке видалення, інакше фізичне.</summary>
    public virtual Task<bool> DeleteAsync(long id, CancellationToken ct = default) =>
        SqlErrorMapper.RunAsync(async () =>
        {
            await EnsureOpenAsync(ct);
            var sql = HasSoftDelete
                ? $"UPDATE {TableName} SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND IsDeleted = 0"
                : $"DELETE FROM {TableName} WHERE Id = @Id";
            return await Connection.ExecuteAsync(Command(sql, new { Id = id }, ct)) == 1;
        });

    /// <summary>
    /// CommandDefinition з поточною транзакцією Unit of Work, таймаутом і CancellationToken.
    /// Кожен виклик Dapper у репозиторіях проходить через цей метод.
    /// </summary>
    protected CommandDefinition Command(
        string sql, object? parameters, CancellationToken ct,
        System.Data.CommandType commandType = System.Data.CommandType.Text) =>
        new(sql, parameters, Transaction, CommandTimeoutSeconds, commandType, cancellationToken: ct);
}
