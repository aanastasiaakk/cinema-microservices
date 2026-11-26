namespace Bookings.Domain.Exceptions;

/// <summary>Базовий доменний виняток. Повідомлення безпечне для показу клієнту (без SQL-деталей).</summary>
public abstract class DomainException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Ресурс не знайдено → HTTP 404.</summary>
public sealed class NotFoundException(string message, Exception? inner = null) : DomainException(message, inner);

/// <summary>
/// Порушення бізнес-правила або конфлікт стану → HTTP 409:
/// недопустимий перехід статусу (THROW 50002), зайняте місце, порушення UNIQUE, конфлікт rowversion.
/// </summary>
public sealed class BusinessConflictException(string message, Exception? inner = null) : DomainException(message, inner);

/// <summary>Бізнес-валідація, що не пройшла (дані коректні за форматом, але не за правилами) → HTTP 400.</summary>
public sealed class ValidationException : DomainException
{
    public ValidationException(string message) : base(message) { }

    public ValidationException(string field, string message) : base(message)
        => Errors = new Dictionary<string, string[]> { [field] = [message] };

    public IReadOnlyDictionary<string, string[]> Errors { get; } = new Dictionary<string, string[]>();
}
