using Catalog.Domain.Entities;
using Catalog.Domain.Exceptions;

namespace Catalog.Tests;

/// <summary>Бізнес-правила, інкапсульовані в сутностях (без БД).</summary>
public class DomainTests
{
    private static readonly DateTime Now = new(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);

    private static Movie NewMovie(decimal price = 150m) => new("Тест", 2024, 100, "12+", price);

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Movie_price_must_be_positive(decimal price)
        => Assert.Throws<DomainException>(() => NewMovie(price));

    [Fact]
    public void Movie_rejects_unknown_age_rating()
        => Assert.Throws<DomainException>(() => new Movie("Тест", 2024, 100, "21+", 100m));

    [Fact]
    public void ChangePrice_validates_and_updates()
    {
        var movie = NewMovie();
        movie.ChangePrice(199.99m);
        Assert.Equal(199.99m, movie.BasePrice);
        Assert.Throws<DomainException>(() => movie.ChangePrice(0));
        Assert.Equal(199.99m, movie.BasePrice);
    }

    [Fact]
    public void Only_one_primary_genre_and_no_duplicates()
    {
        var movie = NewMovie();
        movie.AddGenre(1, isPrimary: false, Now);          // перший жанр стає основним автоматично
        movie.AddGenre(2, isPrimary: true, Now);           // новий основний знімає позначку з попереднього

        Assert.Single(movie.MovieGenres, mg => mg.IsPrimary);
        Assert.True(movie.MovieGenres.Single(mg => mg.GenreId == 2).IsPrimary);
        Assert.Throws<DomainException>(() => movie.AddGenre(1, false, Now));
    }

    [Fact]
    public void Removing_primary_genre_promotes_the_oldest_remaining()
    {
        var movie = NewMovie();
        movie.AddGenre(1, true, Now);
        movie.AddGenre(2, false, Now.AddMinutes(1));
        movie.AddGenre(3, false, Now.AddMinutes(2));

        movie.RemoveGenre(1);

        Assert.True(movie.MovieGenres.Single(mg => mg.GenreId == 2).IsPrimary);
    }

    [Fact]
    public void Showtime_rules_inactive_movie_past_start_and_double_cancel()
    {
        var movie = NewMovie();
        Assert.Throws<DomainException>(() => new Showtime(movie, 1, Now.AddHours(-1), 100m, "uk", Now));
        Assert.Throws<DomainException>(() => new Showtime(movie, 1, Now.AddDays(1), 100m, "de", Now));

        var showtime = new Showtime(movie, 1, Now.AddDays(1), 100m, "uk", Now);
        showtime.Cancel(Now);
        Assert.True(showtime.IsCancelled);
        Assert.Throws<DomainException>(() => showtime.Cancel(Now));
        Assert.Throws<DomainException>(() => showtime.ChangePrice(120m));
    }

    [Fact]
    public void Movie_with_upcoming_showtimes_cannot_be_deactivated()
    {
        var movie = NewMovie();
        movie.Showtimes.Add(new Showtime(movie, 1, Now.AddDays(3), 100m, "uk", Now));

        Assert.Throws<DomainException>(() => movie.Deactivate(Now));
        movie.Showtimes.Single().Cancel(Now);
        movie.Deactivate(Now);
        Assert.False(movie.IsActive);
        Assert.Throws<DomainException>(() => new Showtime(movie, 1, Now.AddDays(5), 100m, "uk", Now));
    }
}
