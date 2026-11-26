using Catalog.Bll.Dto;
using Catalog.Bll.Specifications;
using Catalog.Domain.Entities;

namespace Catalog.Tests;

/// <summary>Специфікації не залежать від EF — їх можна перевірити на колекції в пам'яті (Ardalis Evaluate).</summary>
public class SpecificationTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static List<Movie> Movies()
    {
        var list = new List<Movie>();
        for (var i = 1; i <= 12; i++)
            list.Add(new Movie($"Фільм {i:00}", (short)(2010 + i % 3), 90, i % 2 == 0 ? "12+" : "0+", 100m + i * 10));
        return list;
    }

    [Fact]
    public void Filter_sort_and_page()
    {
        var q = new MovieQuery { Page = 2, PageSize = 3, MinPrice = 130, SortBy = "price", SortDir = "desc" };
        var spec = new MoviesFilterSpec(q, q.PageSize);

        var all = Movies();
        var page = spec.Evaluate(all).ToList();
        var criteriaOnly = all.Where(m => spec.WhereExpressions.All(w => w.FilterFunc(m))).ToList();

        Assert.Equal(10, criteriaOnly.Count);                                // 130…220
        Assert.Equal([190m, 180m, 170m], page.Select(m => m.BasePrice));      // 2-га сторінка, за спаданням
    }

    [Fact]
    public void Search_and_age_rating_filters_combine()
    {
        var spec = new MoviesFilterSpec(new MovieQuery { Search = "Фільм 1", AgeRating = "12+", PageSize = 50 }, 50);
        var titles = spec.Evaluate(Movies()).Select(m => m.Title).ToList();
        Assert.Equal(["Фільм 10", "Фільм 12"], titles);
    }

    [Fact]
    public void Second_specification_uses_the_same_mechanism()
    {
        var movie = new Movie("Сеанси", 2024, 100, "0+", 100m);
        var showtimes = new List<Showtime>
        {
            new(movie, 1, Now.AddDays(3), 150m, "uk", Now),
            new(movie, 2, Now.AddDays(1), 250m, "en", Now),
            new(movie, 1, Now.AddDays(2), 200m, "uk", Now),
        };
        showtimes[0].Cancel(Now);

        var spec = new ShowtimesFilterSpec(new ShowtimeQuery { SortBy = "price", SortDir = "desc", PageSize = 10 }, 10);
        var result = spec.Evaluate(showtimes).ToList();

        Assert.Equal([250m, 200m], result.Select(s => s.Price));             // скасований відфільтровано
    }
}
