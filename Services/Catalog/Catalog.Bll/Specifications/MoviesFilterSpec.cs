using Ardalis.Specification;
using Catalog.Bll.Dto;
using Catalog.Domain.Entities;

namespace Catalog.Bll.Specifications;

/// <summary>
/// Афіша з фільтрами, сортуванням і пагінацією. Специфікація лише ОПИСУЄ запит (Where / Include /
/// OrderBy / Skip / Take) і не залежить від EF — репозиторій застосовує її через WithSpecification
/// і виконує ОДИН SQL-запит (OFFSET … FETCH). Для totalCount той самий об'єкт рахується з
/// evaluateCriteriaOnly: true — лише Where, без Include/OrderBy/Skip/Take.
/// </summary>
public sealed class MoviesFilterSpec : Specification<Movie>
{
    /// <summary>Дозволені поля сортування (усе інше → 400 у валідаторі запиту).</summary>
    public static readonly IReadOnlyList<string> SortFields = ["title", "year", "price", "rating", "createdAt"];

    public MoviesFilterSpec(MovieQuery q, int pageSize)
    {
        Query.AsNoTracking();
        Query.Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre);   // для основного жанру в списку

        if (!string.IsNullOrWhiteSpace(q.Search))
            Query.Where(m => m.Title.Contains(q.Search));
        if (!string.IsNullOrWhiteSpace(q.Genre))
            Query.Where(m => m.MovieGenres.Any(mg => mg.Genre.Slug == q.Genre));
        if (q.Year is not null)
            Query.Where(m => m.ReleaseYear == q.Year);
        if (!string.IsNullOrWhiteSpace(q.AgeRating))
            Query.Where(m => m.AgeRating == q.AgeRating);
        if (q.MinPrice is not null)
            Query.Where(m => m.BasePrice >= q.MinPrice);
        if (q.MaxPrice is not null)
            Query.Where(m => m.BasePrice <= q.MaxPrice);
        if (q.IsActive is not null)
            Query.Where(m => m.IsActive == q.IsActive);

        var desc = string.Equals(q.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        // лише дозволені поля; Id — другий ключ, щоб сторінки були стабільними
        switch (q.SortBy.ToLowerInvariant())
        {
            case "year":
                if (desc) Query.OrderByDescending(m => m.ReleaseYear).ThenBy(m => m.Id);
                else Query.OrderBy(m => m.ReleaseYear).ThenBy(m => m.Id);
                break;
            case "price":
                if (desc) Query.OrderByDescending(m => m.BasePrice).ThenBy(m => m.Id);
                else Query.OrderBy(m => m.BasePrice).ThenBy(m => m.Id);
                break;
            case "rating":
                if (desc) Query.OrderByDescending(m => m.AverageRating).ThenBy(m => m.Id);
                else Query.OrderBy(m => m.AverageRating).ThenBy(m => m.Id);
                break;
            case "createdat":
                if (desc) Query.OrderByDescending(m => m.CreatedAt).ThenBy(m => m.Id);
                else Query.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id);
                break;
            default:
                if (desc) Query.OrderByDescending(m => m.Title).ThenBy(m => m.Id);
                else Query.OrderBy(m => m.Title).ThenBy(m => m.Id);
                break;
        }

        Query.Skip((q.Page - 1) * pageSize).Take(pageSize);
    }
}
