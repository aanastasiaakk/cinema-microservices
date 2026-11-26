using System.ComponentModel.DataAnnotations;

namespace Catalog.Bll.Dto;

public sealed record GenreDto(int Id, string Name, string Slug);

public sealed record GenreMovieDto(long MovieId, string Title, short ReleaseYear, bool IsPrimary, DateTime AddedAt);

public sealed record GenreWithMoviesDto(int Id, string Name, string Slug, IReadOnlyList<GenreMovieDto> Movies);

public sealed record GenreStatsDto(int GenreId, string Genre, int MoviesCount);

public sealed class CreateGenreDto
{
    [Required, StringLength(50)]
    public string Name { get; set; } = "";

    [Required, StringLength(50)]
    public string Slug { get; set; } = "";
}

/// <summary>Фільм, якому жанр додано нещодавно (з полями зв'язку M:N).</summary>
public sealed record RecentGenreMovieDto(long MovieId, string Title, short ReleaseYear, string AgeRating,
                                         decimal BasePrice, DateTime AddedAt, bool IsPrimary);
