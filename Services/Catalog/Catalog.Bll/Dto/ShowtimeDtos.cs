using System.ComponentModel.DataAnnotations;

namespace Catalog.Bll.Dto;

public sealed record HallDto(int Id, string Name, byte Rows, byte SeatsPerRow, string Format, int Capacity);

public sealed record ShowtimeDto(
    long Id, long MovieId, string MovieTitle, int HallId, string HallName, DateTime StartsAt,
    decimal Price, string Language, bool IsCancelled);

public sealed class CreateShowtimeDto
{
    [Range(1, long.MaxValue)] public long MovieId { get; set; }
    [Range(1, int.MaxValue)] public int HallId { get; set; }
    public DateTime StartsAt { get; set; }

    [Range(typeof(decimal), "0.01", "100000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal Price { get; set; }

    [Required] public string Language { get; set; } = "uk";
}

/// <summary>Параметри розкладу: фільтри за фільмом, залом і періодом; сортування startsAt | price.</summary>
public sealed class ShowtimeQuery
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, Paging.MaxPageSize)] public int PageSize { get; set; } = Paging.DefaultPageSize;
    public long? MovieId { get; set; }
    public int? HallId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public bool IncludeCancelled { get; set; }
    public string SortBy { get; set; } = "startsAt";
    public string SortDir { get; set; } = "asc";
}
