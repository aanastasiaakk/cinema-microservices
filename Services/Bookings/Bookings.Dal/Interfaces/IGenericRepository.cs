namespace Bookings.Dal.Interfaces;

/// <summary>
/// Узагальнений CRUD для сутностей з ключем BIGINT Id. Реалізується один раз у BaseDapperRepository&lt;T&gt;.
/// Методи повертають моделі, а не SqlDataReader / DataTable.
/// </summary>
public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetPageAsync(int page, int pageSize, CancellationToken ct = default);
    Task<long> AddAsync(T entity, CancellationToken ct = default);
    Task<bool> UpdateAsync(T entity, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}
