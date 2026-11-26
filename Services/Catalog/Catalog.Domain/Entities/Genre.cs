using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

/// <summary>Довідник жанрів. Початкові записи з фіксованими Id задаються через HasData.</summary>
public class Genre
{
    public int Id { get; private set; }
    public string Name { get; private set; } = "";
    /// <summary>Частина URL сторінки жанру: /genre/{slug}. Лише a-z, 0-9 і дефіс.</summary>
    public string Slug { get; private set; } = "";

    /// <summary>Зворотний бік M:N через проміжну сутність MovieGenre.</summary>
    public List<MovieGenre> MovieGenres { get; } = [];

    private Genre() { }   // для EF Core

    public Genre(string name, string slug)
    {
        Rename(name);
        SetSlug(slug);
    }

    /// <summary>Конструктор для сідінгу довідника з фіксованим Id (HasData).</summary>
    internal Genre(int id, string name, string slug) : this(name, slug) => Id = id;

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Genre name is required");
        if (name.Length > 50) throw new DomainException("Genre name must be at most 50 characters");
        Name = name.Trim();
    }

    private void SetSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 50 ||
            slug.Any(ch => !(ch is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')))
            throw new DomainException("Slug must contain only a-z, 0-9 and '-' (max 50)");
        Slug = slug;
    }

    public static Genre Seed(int id, string name, string slug) => new(id, name, slug);
}
