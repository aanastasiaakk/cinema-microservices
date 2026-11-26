using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Catalog.Dal.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

/// <summary>Async Generic Repository на DbSet&lt;T&gt;. Не викликає SaveChanges — це робить Unit of Work.</summary>
public class GenericRepository<T>(CatalogDbContext context) : IGenericRepository<T> where T : class
{
    protected readonly CatalogDbContext Context = context;
    protected readonly DbSet<T> Set = context.Set<T>();

    public virtual async Task<T?> GetByIdAsync(object id, CancellationToken ct = default)
        => await Set.FindAsync([id], ct);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await Set.AsNoTracking().ToListAsync(ct);

    public virtual async Task AddAsync(T entity, CancellationToken ct = default)
        => await Set.AddAsync(entity, ct);

    /// <summary>Лише позначає сутність як змінену; у БД потрапить після SaveChangesAsync.</summary>
    public virtual void Update(T entity) => Set.Update(entity);

    /// <summary>Лише позначає сутність на видалення; у БД потрапить після SaveChangesAsync.</summary>
    public virtual void Delete(T entity) => Set.Remove(entity);

    // ---------- Specification pattern: репозиторій лише ЗАСТОСОВУЄ специфікацію ----------

    public virtual async Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken ct = default)
        => await Set.WithSpecification(specification).ToListAsync(ct);

    public virtual async Task<IReadOnlyList<TResult>> ListAsync<TResult>(
        ISpecification<T, TResult> specification, CancellationToken ct = default)
        => await Set.WithSpecification(specification).ToListAsync(ct);

    public virtual Task<int> CountAsync(ISpecification<T> specification, CancellationToken ct = default)
        // evaluateCriteriaOnly: лише Where — без Include/OrderBy/Skip/Take, тобто SELECT COUNT(*) за фільтром
        => SpecificationEvaluator.Default.GetQuery(Set.AsQueryable(), specification, evaluateCriteriaOnly: true)
                                 .CountAsync(ct);

    public virtual Task<T?> FirstOrDefaultAsync(ISpecification<T> specification, CancellationToken ct = default)
        => Set.WithSpecification(specification).FirstOrDefaultAsync(ct);
}
