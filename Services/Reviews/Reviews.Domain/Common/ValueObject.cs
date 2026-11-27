namespace Reviews.Domain.Common;

/// <summary>
/// Базовий Value Object (як ValueObject в eShop, Ordering.Domain/SeedWork): незмінний,
/// без ідентичності, порівнюється за значеннями компонентів, а не за посиланням.
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public bool Equals(ValueObject? other)
        => other is not null && GetType() == other.GetType()
           && GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());

    public override bool Equals(object? obj) => obj is ValueObject other && Equals(other);

    public override int GetHashCode()
        => GetEqualityComponents().Aggregate(0, (hash, part) => HashCode.Combine(hash, part));

    public static bool operator ==(ValueObject? left, ValueObject? right) => Equals(left, right);
    public static bool operator !=(ValueObject? left, ValueObject? right) => !Equals(left, right);
}
