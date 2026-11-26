using AutoMapper;
using Catalog.Bll.Dto;
using Catalog.Bll.Mapping;
using Catalog.Bll.Validation;
using Microsoft.Extensions.Logging.Abstractions;

namespace Catalog.Tests;

public class ValidationAndMappingTests
{
    [Fact]
    public void AutoMapper_configuration_is_valid()
    {
        var config = new MapperConfiguration(cfg => cfg.AddMaps(typeof(MovieProfile).Assembly), NullLoggerFactory.Instance);
        config.AssertConfigurationIsValid();
    }

    [Fact]
    public void CreateMovie_validator_reports_complex_rules_per_field()
    {
        var validator = new CreateMovieDtoValidator(TimeProvider.System);
        var result = validator.Validate(new CreateMovieDto
        {
            Title = "X", ReleaseYear = 1700, DurationMin = 90, AgeRating = "21+", BasePrice = 10.555m,
            Description = "d", TrailerUrl = "http://insecure", GenreIds = [1, 1], PrimaryGenreId = 3
        });

        var fields = result.Errors.Select(e => e.PropertyName).Distinct().ToList();
        Assert.Contains(nameof(CreateMovieDto.ReleaseYear), fields);
        Assert.Contains(nameof(CreateMovieDto.AgeRating), fields);
        Assert.Contains(nameof(CreateMovieDto.BasePrice), fields);
        Assert.Contains(nameof(CreateMovieDto.TrailerUrl), fields);
        Assert.Contains(nameof(CreateMovieDto.GenreIds), fields);
        Assert.Contains(nameof(CreateMovieDto.PrimaryGenreId), fields);
    }

    [Theory]
    [InlineData("title", "asc", true)]
    [InlineData("Price", "DESC", true)]
    [InlineData("hack; DROP TABLE", "asc", false)]
    [InlineData("year", "sideways", false)]
    public void MovieQuery_allows_only_whitelisted_sorting(string sortBy, string sortDir, bool valid)
        => Assert.Equal(valid, new MovieQueryValidator().Validate(new MovieQuery { SortBy = sortBy, SortDir = sortDir }).IsValid);
}
