using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

/// <summary>1:1 з Movie: «важкі» рідко потрібні поля. PK = FK MovieId.</summary>
public class MovieDetail
{
    public long MovieId { get; private set; }
    public string Description { get; private set; } = "";
    public string? Director { get; private set; }
    public string? Country { get; private set; }
    public string? TrailerUrl { get; private set; }
    public string? PosterUrl { get; private set; }

    private MovieDetail() { }   // для EF Core

    internal MovieDetail(long movieId, string description, string? director, string? country,
                         string? trailerUrl, string? posterUrl)
    {
        MovieId = movieId;
        Update(description, director, country, trailerUrl, posterUrl);
    }

    internal void Update(string description, string? director, string? country, string? trailerUrl, string? posterUrl)
    {
        if (string.IsNullOrWhiteSpace(description)) throw new DomainException("Description is required");
        if (trailerUrl is not null && !trailerUrl.StartsWith("https://", StringComparison.Ordinal))
            throw new DomainException("Trailer URL must start with https://");
        Description = description.Trim();
        Director = director;
        Country = country;
        TrailerUrl = trailerUrl;
        PosterUrl = posterUrl;
    }
}
