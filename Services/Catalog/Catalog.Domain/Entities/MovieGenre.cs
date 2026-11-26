namespace Catalog.Domain.Entities;

/// <summary>
/// Явна проміжна сутність M:N Movie ↔ Genre. Складений ключ (MovieId, GenreId) і ВЛАСНІ поля
/// AddedAt та IsPrimary — автоматичне M:N EF Core їх зберегти не дозволило б.
/// </summary>
public class MovieGenre
{
    public long MovieId { get; private set; }
    public Movie Movie { get; private set; } = null!;
    public int GenreId { get; private set; }
    public Genre Genre { get; private set; } = null!;
    /// <summary>Коли жанр додано фільму (власне поле зв'язку).</summary>
    public DateTime AddedAt { get; private set; }
    /// <summary>Основний жанр фільму (у фільму не більше одного).</summary>
    public bool IsPrimary { get; private set; }

    private MovieGenre() { }   // для EF Core

    internal MovieGenre(Movie movie, int genreId, bool isPrimary, DateTime addedAt)
    {
        Movie = movie;
        MovieId = movie.Id;
        GenreId = genreId;
        IsPrimary = isPrimary;
        AddedAt = addedAt;
    }

    internal void MarkPrimary() => IsPrimary = true;
    internal void UnmarkPrimary() => IsPrimary = false;
}
