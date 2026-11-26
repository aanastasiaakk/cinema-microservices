using System.ComponentModel.DataAnnotations;

namespace Catalog.Bll.Dto;

// ---------- Вихідні DTO (без логіки) ----------

public sealed record MovieListItemDto(
    long Id, string Title, short ReleaseYear, short DurationMin, string AgeRating,
    decimal BasePrice, bool IsActive, decimal? AverageRating, int ReviewsCount, string? PrimaryGenre);

public sealed record MovieGenreDto(int GenreId, string Name, string Slug, bool IsPrimary, DateTime AddedAt);

public sealed record MovieDetailDto(string Description, string? Director, string? Country, string? TrailerUrl, string? PosterUrl);

public sealed record MovieDto(
    long Id, string Title, short ReleaseYear, short DurationMin, string AgeRating, decimal BasePrice,
    bool IsActive, decimal? AverageRating, int ReviewsCount, DateTime CreatedAt, DateTime? UpdatedAt,
    MovieDetailDto? Detail, IReadOnlyList<MovieGenreDto> Genres, IReadOnlyList<ShowtimeDto> Showtimes);

// ---------- Вхідні DTO: ПРОСТІ правила — DataAnnotations, складні — FluentValidation (Validation/) ----------

public sealed class CreateMovieDto
{
    [Required, StringLength(200)]
    public string Title { get; set; } = "";

    public short ReleaseYear { get; set; }

    [Range(1, 600)]
    public short DurationMin { get; set; }

    [Required]
    public string AgeRating { get; set; } = "0+";

    [Range(typeof(decimal), "0.01", "100000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal BasePrice { get; set; }

    [Required, StringLength(2000)]
    public string Description { get; set; } = "";

    [StringLength(200)] public string? Director { get; set; }
    [StringLength(100)] public string? Country { get; set; }
    [StringLength(500)] public string? TrailerUrl { get; set; }
    [StringLength(500)] public string? PosterUrl { get; set; }

    /// <summary>Жанри фільму; перший або <see cref="PrimaryGenreId"/> стає основним.</summary>
    public List<int> GenreIds { get; set; } = [];
    public int? PrimaryGenreId { get; set; }
}

public sealed class ChangePriceDto
{
    [Range(typeof(decimal), "0.01", "100000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal Price { get; set; }
}

public sealed class AddMovieGenreDto
{
    [Range(1, int.MaxValue)]
    public int GenreId { get; set; }
    public bool IsPrimary { get; set; }
}

/// <summary>
/// Параметри колекції фільмів: page (з 1), pageSize (за замовчуванням 20, максимум 100),
/// фільтри та сортування лише за дозволеними полями (sortBy: title | year | price | rating | createdAt).
/// </summary>
public sealed class MovieQuery
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, Paging.MaxPageSize)] public int PageSize { get; set; } = Paging.DefaultPageSize;
    [StringLength(100)] public string? Search { get; set; }
    [StringLength(50)] public string? Genre { get; set; }
    public short? Year { get; set; }
    public string? AgeRating { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool? IsActive { get; set; }
    public string SortBy { get; set; } = "title";
    public string SortDir { get; set; } = "asc";
}
