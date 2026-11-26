using System.Data;
using Bookings.Dal.Infrastructure;
using Bookings.Dal.Interfaces;
using Bookings.Domain.Models;
using Microsoft.Data.SqlClient;

namespace Bookings.Dal.Repositories;

/// <summary>
/// Репозиторій на ЧИСТОМУ ADO.NET (Microsoft.Data.SqlClient) — без Dapper і без Generic Repository.
/// Показує: SqlCommand з явними типізованими параметрами, ручне читання SqlDataReader,
/// роботу в спільному з'єднанні/транзакції Unit of Work, await using для команд і reader-ів.
/// Жодної конкатенації вхідних даних у SQL — лише параметри.
/// </summary>
public sealed class CustomerRepository : RepositoryBase, ICustomerRepository
{
    private const string SelectColumns =
        "Id, Email, FullName, Phone, CreatedAt, UpdatedAt, IsDeleted, RowVersion";

    public CustomerRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(connection, currentTransaction) { }

    public Task<Customer?> GetByIdAsync(long id, CancellationToken ct = default) =>
        SqlErrorMapper.RunAsync(async () =>
        {
            const string sql = $"SELECT {SelectColumns} FROM dbo.Customers WHERE Id = @Id AND IsDeleted = 0";
            await EnsureOpenAsync(ct);
            await using var command = CreateCommand(sql);
            command.Parameters.Add("@Id", SqlDbType.BigInt).Value = id;
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
            return await reader.ReadAsync(ct) ? Map(reader) : null;
        });

    public Task<Customer?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        SqlErrorMapper.RunAsync(async () =>
        {
            const string sql = $"SELECT {SelectColumns} FROM dbo.Customers WHERE Email = @Email";
            await EnsureOpenAsync(ct);
            await using var command = CreateCommand(sql);
            command.Parameters.Add("@Email", SqlDbType.NVarChar, 256).Value = email;
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
            return await reader.ReadAsync(ct) ? Map(reader) : null;
        });

    public Task<IReadOnlyList<Customer>> SearchAsync(string? name, int page, int pageSize, CancellationToken ct = default) =>
        SqlErrorMapper.RunAsync<IReadOnlyList<Customer>>(async () =>
        {
            // LIKE-шаблон теж передається параметром: '%' + @Name + '%' будує сервер, а не C#
            const string sql = $@"
                SELECT {SelectColumns}
                FROM dbo.Customers
                WHERE IsDeleted = 0
                  AND (@Name IS NULL OR FullName LIKE N'%' + @Name + N'%')
                ORDER BY FullName, Id
                OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";
            await EnsureOpenAsync(ct);
            await using var command = CreateCommand(sql);
            command.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = (object?)name ?? DBNull.Value;
            command.Parameters.Add("@Skip", SqlDbType.Int).Value = (page - 1) * pageSize;
            command.Parameters.Add("@Take", SqlDbType.Int).Value = pageSize;

            var result = new List<Customer>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                result.Add(Map(reader));
            return result;
        });

    public Task<long> AddAsync(Customer customer, string user, CancellationToken ct = default) =>
        SqlErrorMapper.RunAsync(async () =>
        {
            const string sql = @"
                INSERT INTO dbo.Customers (Email, FullName, Phone, CreatedBy)
                OUTPUT INSERTED.Id
                VALUES (@Email, @FullName, @Phone, @User)";
            await EnsureOpenAsync(ct);
            await using var command = CreateCommand(sql);
            command.Parameters.Add("@Email", SqlDbType.NVarChar, 256).Value = customer.Email;
            command.Parameters.Add("@FullName", SqlDbType.NVarChar, 200).Value = customer.FullName;
            command.Parameters.Add("@Phone", SqlDbType.NVarChar, 20).Value = (object?)customer.Phone ?? DBNull.Value;
            command.Parameters.Add("@User", SqlDbType.NVarChar, 100).Value = user;
            return (long)(await command.ExecuteScalarAsync(ct))!;
        });

    public Task<bool> UpdateAsync(Customer customer, string user, CancellationToken ct = default) =>
        SqlErrorMapper.RunAsync(async () =>
        {
            // Оптимістична конкурентність: WHERE RowVersion = @RowVersion
            const string sql = @"
                UPDATE dbo.Customers
                SET Email = @Email, FullName = @FullName, Phone = @Phone,
                    UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @User
                WHERE Id = @Id AND IsDeleted = 0 AND RowVersion = @RowVersion";
            await EnsureOpenAsync(ct);
            await using var command = CreateCommand(sql);
            command.Parameters.Add("@Id", SqlDbType.BigInt).Value = customer.Id;
            command.Parameters.Add("@Email", SqlDbType.NVarChar, 256).Value = customer.Email;
            command.Parameters.Add("@FullName", SqlDbType.NVarChar, 200).Value = customer.FullName;
            command.Parameters.Add("@Phone", SqlDbType.NVarChar, 20).Value = (object?)customer.Phone ?? DBNull.Value;
            command.Parameters.Add("@User", SqlDbType.NVarChar, 100).Value = user;
            command.Parameters.Add("@RowVersion", SqlDbType.Timestamp).Value = customer.RowVersion;
            return await command.ExecuteNonQueryAsync(ct) == 1;
        });

    public Task<bool> SoftDeleteAsync(long id, string user, CancellationToken ct = default) =>
        SqlErrorMapper.RunAsync(async () =>
        {
            const string sql = @"
                UPDATE dbo.Customers
                SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @User
                WHERE Id = @Id AND IsDeleted = 0";
            await EnsureOpenAsync(ct);
            await using var command = CreateCommand(sql);
            command.Parameters.Add("@Id", SqlDbType.BigInt).Value = id;
            command.Parameters.Add("@User", SqlDbType.NVarChar, 100).Value = user;
            return await command.ExecuteNonQueryAsync(ct) == 1;
        });

    public Task<bool> HasActiveBookingsAsync(long customerId, CancellationToken ct = default) =>
        SqlErrorMapper.RunAsync(async () =>
        {
            const string sql = @"
                SELECT CASE WHEN EXISTS (
                    SELECT 1 FROM dbo.Bookings
                    WHERE CustomerId = @CustomerId AND IsDeleted = 0
                      AND Status IN (N'Created', N'AwaitingPayment', N'Paid')) THEN 1 ELSE 0 END";
            await EnsureOpenAsync(ct);
            await using var command = CreateCommand(sql);
            command.Parameters.Add("@CustomerId", SqlDbType.BigInt).Value = customerId;
            return (int)(await command.ExecuteScalarAsync(ct))! == 1;
        });

    /// <summary>Команда в спільному з'єднанні і ПОТОЧНІЙ транзакції Unit of Work.</summary>
    private SqlCommand CreateCommand(string sql) =>
        new(sql, Connection, Transaction) { CommandTimeout = CommandTimeoutSeconds };

    /// <summary>Ручний мапінг рядка reader-а на модель (за порядком колонок SelectColumns).</summary>
    private static Customer Map(SqlDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        Email = reader.GetString(1),
        FullName = reader.GetString(2),
        Phone = reader.IsDBNull(3) ? null : reader.GetString(3),
        CreatedAt = reader.GetDateTime(4),
        UpdatedAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
        IsDeleted = reader.GetBoolean(6),
        RowVersion = (byte[])reader[7]
    };
}
