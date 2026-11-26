namespace Catalog.Domain.Exceptions;

/// <summary>
/// Порушення бізнес-правила самої сутності (ціна ≤ 0, скасування вже скасованого сеансу тощо).
/// Глобальний обробник API перетворює його на 409 Conflict.
/// </summary>
public class DomainException(string message) : Exception(message);

/// <summary>Ресурс не знайдено → 404.</summary>
public sealed class EntityNotFoundException(string entity, object key)
    : Exception($"{entity} with id '{key}' was not found")
{
    public string Entity { get; } = entity;
    public object Key { get; } = key;
}

/// <summary>Конфлікт зі станом БД (дубль назви, зайнятий зал) → 409.</summary>
public sealed class ConflictException(string message) : Exception(message);

/// <summary>Помилка валідації вхідних даних, виявлена в BLL → 400 (ValidationProblemDetails).</summary>
public sealed class ValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred")
{
    public ValidationException(string field, string error)
        : this(new Dictionary<string, string[]> { [field] = [error] }) { }

    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
