using Reviews.Domain.Common;
using Reviews.Domain.Exceptions;

namespace Reviews.Domain.ValueObjects;

/// <summary>
/// Автор (вкладений документ author): Id клієнта з контексту Booking (без FK) і копія його імені.
/// Незмінний: без setter-ів, валідація в конструкторі.
/// </summary>
public sealed class Author : ValueObject
{
    public Author(long customerId, string displayName)
    {
        if (customerId <= 0) throw new DomainException("Author customerId must be positive");
        if (string.IsNullOrWhiteSpace(displayName)) throw new DomainException("Author displayName is required");
        if (displayName.Length > 100) throw new DomainException("Author displayName must be at most 100 characters");
        CustomerId = customerId;
        DisplayName = displayName.Trim();
    }

    public long CustomerId { get; }
    public string DisplayName { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CustomerId;
        yield return DisplayName;
    }

    public override string ToString() => $"{DisplayName} (#{CustomerId})";
}
