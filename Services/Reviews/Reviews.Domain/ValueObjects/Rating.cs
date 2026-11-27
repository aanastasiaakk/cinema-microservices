using Reviews.Domain.Common;
using Reviews.Domain.Exceptions;

namespace Reviews.Domain.ValueObjects;

/// <summary>Оцінка 1–5. Незмінна; у документі зберігається як звичайне число "rating" (власний серіалізатор в Infrastructure).</summary>
public sealed class Rating : ValueObject, IComparable<Rating>
{
    public const int Min = 1;
    public const int Max = 5;

    public Rating(int value)
    {
        if (value is < Min or > Max) throw new DomainException($"Rating must be between {Min} and {Max}");
        Value = value;
    }

    public int Value { get; }

    public bool IsPositive => Value >= 4;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public int CompareTo(Rating? other) => other is null ? 1 : Value.CompareTo(other.Value);

    public static implicit operator int(Rating rating) => rating.Value;
    public override string ToString() => $"{Value}/{Max}";
}
