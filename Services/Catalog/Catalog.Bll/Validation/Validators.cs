using Catalog.Bll.Dto;
using Catalog.Bll.Specifications;
using Catalog.Domain.Entities;
using FluentValidation;

namespace Catalog.Bll.Validation;

// Складні правила — тут; прості (Required, StringLength, Range) — DataAnnotations на DTO, без дублювання.
// Валідатори реєструються AddValidatorsFromAssemblyContaining<…>() і викликаються ЯВНО в контролерах
// (застарілий AddFluentValidation не використовується).

public sealed class CreateMovieDtoValidator : AbstractValidator<CreateMovieDto>
{
    public CreateMovieDtoValidator(TimeProvider clock)
    {
        RuleFor(x => x.ReleaseYear)
            .InclusiveBetween((short)Movie.MinYear, (short)(clock.GetUtcNow().Year + 5))
            .WithMessage(_ => $"Release year must be between {Movie.MinYear} and {clock.GetUtcNow().Year + 5}");
        RuleFor(x => x.AgeRating)
            .Must(AgeRatings.IsValid)
            .WithMessage($"Age rating must be one of: {string.Join(", ", AgeRatings.All)}");
        RuleFor(x => x.BasePrice)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(x => x.TrailerUrl)
            .Must(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            .When(x => x.TrailerUrl is not null)
            .WithMessage("Trailer URL must be an absolute https:// URL");
        RuleFor(x => x.PosterUrl)
            .Must(u => Uri.TryCreate(u, UriKind.Absolute, out _))
            .When(x => x.PosterUrl is not null)
            .WithMessage("Poster URL must be an absolute URL");
        RuleFor(x => x.GenreIds)
            .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("Genre ids must be unique")
            .Must(ids => ids.Count <= 5).WithMessage("A movie can have at most 5 genres");
        RuleForEach(x => x.GenreIds).GreaterThan(0);
        RuleFor(x => x.PrimaryGenreId)
            .Must((dto, id) => dto.GenreIds.Contains(id!.Value))
            .When(x => x.PrimaryGenreId is not null)
            .WithMessage("Primary genre must be one of GenreIds");
    }
}

public sealed class MovieQueryValidator : AbstractValidator<MovieQuery>
{
    public MovieQueryValidator()
    {
        RuleFor(x => x.SortBy)
            .Must(s => MoviesFilterSpec.SortFields.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"sortBy must be one of: {string.Join(", ", MoviesFilterSpec.SortFields)}");
        RuleFor(x => x.SortDir).Must(BeSortDirection).WithMessage("sortDir must be 'asc' or 'desc'");
        RuleFor(x => x.AgeRating).Must(AgeRatings.IsValid).When(x => x.AgeRating is not null)
            .WithMessage($"ageRating must be one of: {string.Join(", ", AgeRatings.All)}");
        RuleFor(x => x.MinPrice).GreaterThanOrEqualTo(0).When(x => x.MinPrice is not null);
        RuleFor(x => x.MaxPrice)
            .GreaterThanOrEqualTo(x => x.MinPrice!.Value)
            .When(x => x.MinPrice is not null && x.MaxPrice is not null)
            .WithMessage("maxPrice must be greater than or equal to minPrice");
    }

    internal static bool BeSortDirection(string dir) =>
        string.Equals(dir, "asc", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
}

public sealed class CreateGenreDtoValidator : AbstractValidator<CreateGenreDto>
{
    public CreateGenreDtoValidator()
    {
        RuleFor(x => x.Slug).Matches("^[a-z0-9]+(-[a-z0-9]+)*$")
            .WithMessage("Slug must contain only lowercase latin letters, digits and single dashes");
    }
}

public sealed class CreateShowtimeDtoValidator : AbstractValidator<CreateShowtimeDto>
{
    public CreateShowtimeDtoValidator(TimeProvider clock)
    {
        RuleFor(x => x.StartsAt)
            .Must(t => t > clock.GetUtcNow().UtcDateTime).WithMessage("Showtime must start in the future")
            .Must(t => t.Second == 0 && t.Millisecond == 0).WithMessage("Start time must be whole minutes");
        RuleFor(x => x.Language).Must(l => Showtime.Languages.Contains(l))
            .WithMessage("Language must be 'uk' or 'en'");
    }
}

public sealed class ShowtimeQueryValidator : AbstractValidator<ShowtimeQuery>
{
    public ShowtimeQueryValidator()
    {
        RuleFor(x => x.SortBy)
            .Must(s => ShowtimesFilterSpec.SortFields.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"sortBy must be one of: {string.Join(", ", ShowtimesFilterSpec.SortFields)}");
        RuleFor(x => x.SortDir).Must(MovieQueryValidator.BeSortDirection).WithMessage("sortDir must be 'asc' or 'desc'");
        RuleFor(x => x.To).GreaterThan(x => x.From!.Value)
            .When(x => x.From is not null && x.To is not null)
            .WithMessage("'to' must be later than 'from'");
    }
}
