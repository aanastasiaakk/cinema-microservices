namespace Reviews.Domain.Exceptions;

/// <summary>Порушення бізнес-правила сутності або Value Object → 400 Bad Request.</summary>
public class DomainException(string message) : Exception(message);

/// <summary>Документ не знайдено → 404 Not Found.</summary>
public sealed class NotFoundException(string entity, object key)
    : Exception($"{entity} '{key}' was not found");

/// <summary>Конфлікт, зокрема конфлікт версій (оптимістична конкурентність) → 409 Conflict.</summary>
public sealed class ConflictException(string message) : Exception(message);
