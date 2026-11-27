using Reviews.Domain.Common;
using Reviews.Domain.Exceptions;

namespace Reviews.Domain.ValueObjects;

/// <summary>Дані критика (вкладений документ criticInfo): видання і необов'язкова оцінка 0–100.</summary>
public sealed class CriticInfo : ValueObject
{
    public CriticInfo(string outlet, int? score100)
    {
        if (string.IsNullOrWhiteSpace(outlet)) throw new DomainException("Critic outlet is required");
        if (score100 is < 0 or > 100) throw new DomainException("Critic score must be between 0 and 100");
        Outlet = outlet.Trim();
        Score100 = score100;
    }

    public string Outlet { get; }
    public int? Score100 { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Outlet;
        yield return Score100;
    }
}
