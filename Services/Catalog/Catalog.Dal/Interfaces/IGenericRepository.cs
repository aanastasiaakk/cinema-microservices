using Ardalis.Specification;

namespace Catalog.Dal.Interfaces;

/// <summary>
/// Async Generic Repository — той самий контракт, що в Лаб. 02, але реалізований на DbSet&lt;T&gt;.
/// Усі звернення до БД асинхронні. Update/Delete лише змінюють стан у контексті (синхронні),
/// зберігає зміни ТІЛЬКИ Unit of Work (SaveChangesAsync) — репозиторії самі нічого не зберігають.
/// Методи зі специфікацією застосовують будь-яку ISpecification&lt;T&gt; без змін у репозиторії.
/// </summary>
public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(object id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Delete(T entity);

    /// <summary>Вибірка за специфікацією: Where / Include / OrderBy / Skip / Take в одному SQL-запиті.</summary>
    Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken ct = default);
    /// <summary>Проєкція за специфікацією з Select.</summary>
    Task<IReadOnlyList<TResult>> ListAsync<TResult>(ISpecification<T, TResult> specification, CancellationToken ct = default);
    /// <summary>Кількість за критеріями специфікації (Skip/Take/OrderBy ігноруються) — для totalCount.</summary>
    Task<int> CountAsync(ISpecification<T> specification, CancellationToken ct = default);
    Task<T?> FirstOrDefaultAsync(ISpecification<T> specification, CancellationToken ct = default);
}
