namespace Catalog.Domain.Entities;

/// <summary>Допустимі вікові рейтинги (збігаються з CHECK-обмеженням у БД).</summary>
public static class AgeRatings
{
    public static readonly IReadOnlyList<string> All = ["0+", "6+", "12+", "16+", "18+"];
    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}

/// <summary>Формати залів.</summary>
public static class HallFormats
{
    public static readonly IReadOnlyList<string> All = ["2D", "3D", "IMAX", "4DX"];
    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}
