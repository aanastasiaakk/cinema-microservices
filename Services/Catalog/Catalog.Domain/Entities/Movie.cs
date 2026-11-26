using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

/// <summary>
/// Фільм — корінь агрегату (Movie + MovieDetail + MovieGenres), аналог CatalogItem з eShop.
/// Приватні setter-и: стан змінюється лише методами, які перевіряють бізнес-правила.
/// </summary>
public class Movie
{
    public const int MinYear = 1895;

    public long Id { get; private set; }
    public string Title { get; private set; } = "";
    public short ReleaseYear { get; private set; }
    public short DurationMin { get; private set; }
    public string AgeRating { get; private set; } = "0+";
    public decimal BasePrice { get; private set; }
    public bool IsActive { get; private set; } = true;
    /// <summary>Копія з контексту Reviews (MongoDB); змінюється лише через <see cref="UpdateRating"/>.</summary>
    public decimal? AverageRating { get; private set; }
    public int ReviewsCount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    /// <summary>Маркер конкурентності (rowversion).</summary>
    public byte[] RowVersion { get; private set; } = [];

    public MovieDetail? Detail { get; private set; }                    // 1:1
    public List<Showtime> Showtimes { get; } = [];                      // 1:N
    public List<MovieGenre> MovieGenres { get; } = [];                  // M:N через проміжну сутність

    private Movie() { }   // для EF Core

    public Movie(string title, short releaseYear, short durationMin, string ageRating, decimal basePrice)
    {
        Rename(title);
        if (releaseYear is < MinYear or > 2100)
            throw new DomainException($"Release year must be between {MinYear} and 2100");
        if (durationMin is < 1 or > 600)
            throw new DomainException("Duration must be between 1 and 600 minutes");
        if (!AgeRatings.IsValid(ageRating))
            throw new DomainException($"Age rating must be one of: {string.Join(", ", AgeRatings.All)}");

        ReleaseYear = releaseYear;
        DurationMin = durationMin;
        AgeRating = ageRating;
        ChangePriceCore(basePrice);
    }

    public void Rename(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Title is required");
        if (title.Length > 200) throw new DomainException("Title must be at most 200 characters");
        Title = title.Trim();
        Touch();
    }

    /// <summary>Зміна базової ціни квитка (правило: ціна &gt; 0).</summary>
    public void ChangePrice(decimal newPrice)
    {
        ChangePriceCore(newPrice);
        Touch();
    }

    private void ChangePriceCore(decimal price)
    {
        if (price <= 0) throw new DomainException("Price must be positive");
        if (decimal.Round(price, 2) != price) throw new DomainException("Price must have at most 2 decimal places");
        BasePrice = price;
    }

    /// <summary>Зняти з афіші. Правило: не можна, поки є не скасовані майбутні сеанси.</summary>
    public void Deactivate(DateTime nowUtc)
    {
        if (!IsActive) throw new DomainException("Movie is already inactive");
        if (Showtimes.Any(s => !s.IsCancelled && s.StartsAt > nowUtc))
            throw new DomainException("Movie has upcoming showtimes; cancel them first");
        IsActive = false;
        Touch();
    }

    public void Activate()
    {
        if (IsActive) throw new DomainException("Movie is already active");
        IsActive = true;
        Touch();
    }

    /// <summary>Заповнити або змінити «важкі» поля (1:1).</summary>
    public void SetDetail(string description, string? director, string? country, string? trailerUrl, string? posterUrl)
    {
        if (Detail is null) Detail = new MovieDetail(Id, description, director, country, trailerUrl, posterUrl);
        else Detail.Update(description, director, country, trailerUrl, posterUrl);
        Touch();
    }

    /// <summary>
    /// Додати жанр (M:N). Правила: жанр не повторюється; основний жанр лише один —
    /// якщо новий жанр основний, попередній основний перестає бути основним.
    /// </summary>
    public MovieGenre AddGenre(int genreId, bool isPrimary, DateTime nowUtc)
    {
        if (MovieGenres.Any(mg => mg.GenreId == genreId))
            throw new DomainException($"Genre {genreId} is already assigned to the movie");
        if (isPrimary)
            foreach (var mg in MovieGenres.Where(mg => mg.IsPrimary)) mg.UnmarkPrimary();

        var link = new MovieGenre(this, genreId, isPrimary || MovieGenres.Count == 0, nowUtc);
        MovieGenres.Add(link);
        Touch();
        return link;
    }

    public void RemoveGenre(int genreId)
    {
        var link = MovieGenres.FirstOrDefault(mg => mg.GenreId == genreId)
                   ?? throw new DomainException($"Genre {genreId} is not assigned to the movie");
        MovieGenres.Remove(link);
        // якщо прибрали основний — основним стає найстаріший із решти
        if (link.IsPrimary && MovieGenres.Count > 0)
            MovieGenres.OrderBy(mg => mg.AddedAt).First().MarkPrimary();
        Touch();
    }

    /// <summary>Оновлення копії рейтингу з контексту Reviews (подія MovieRatingChanged).</summary>
    public void UpdateRating(decimal? averageRating, int reviewsCount)
    {
        if (averageRating is < 1 or > 5) throw new DomainException("Average rating must be between 1 and 5");
        if (reviewsCount < 0) throw new DomainException("Reviews count cannot be negative");
        AverageRating = averageRating;
        ReviewsCount = reviewsCount;
        Touch();
    }

    private void Touch()
    {
        if (Id != 0) UpdatedAt = DateTime.UtcNow;
    }
}
