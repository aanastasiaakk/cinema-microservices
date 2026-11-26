using AutoMapper;
using Catalog.Bll.Dto;
using Catalog.Domain.Entities;

namespace Catalog.Bll.Mapping;

/// <summary>Профілі зібрані в BLL і реєструються в DI одним AddAutoMapper(...) за збіркою.</summary>
public sealed class MovieProfile : Profile
{
    public MovieProfile()
    {
        CreateMap<Movie, MovieListItemDto>()
            .ForCtorParam(nameof(MovieListItemDto.PrimaryGenre), o => o.MapFrom(m =>
                m.MovieGenres.Where(mg => mg.IsPrimary).Select(mg => mg.Genre.Name).FirstOrDefault()));

        CreateMap<MovieDetail, MovieDetailDto>();

        CreateMap<MovieGenre, MovieGenreDto>()
            .ForCtorParam(nameof(MovieGenreDto.Name), o => o.MapFrom(mg => mg.Genre.Name))
            .ForCtorParam(nameof(MovieGenreDto.Slug), o => o.MapFrom(mg => mg.Genre.Slug));

        CreateMap<Movie, MovieDto>()
            .ForCtorParam(nameof(MovieDto.Genres), o => o.MapFrom(m =>
                m.MovieGenres.OrderByDescending(mg => mg.IsPrimary).ThenBy(mg => mg.AddedAt)))
            .ForCtorParam(nameof(MovieDto.Showtimes), o => o.MapFrom(m => m.Showtimes.OrderBy(s => s.StartsAt)));
    }
}

public sealed class GenreProfile : Profile
{
    public GenreProfile()
    {
        CreateMap<Genre, GenreDto>();
        CreateMap<Catalog.Dal.Interfaces.RecentGenreMovie, RecentGenreMovieDto>();
        CreateMap<MovieGenre, GenreMovieDto>()
            .ForCtorParam(nameof(GenreMovieDto.Title), o => o.MapFrom(mg => mg.Movie.Title))
            .ForCtorParam(nameof(GenreMovieDto.ReleaseYear), o => o.MapFrom(mg => mg.Movie.ReleaseYear));
        CreateMap<Genre, GenreWithMoviesDto>()
            .ForCtorParam(nameof(GenreWithMoviesDto.Movies), o => o.MapFrom(g =>
                g.MovieGenres.OrderByDescending(mg => mg.AddedAt)));
    }
}

public sealed class ShowtimeProfile : Profile
{
    public ShowtimeProfile()
    {
        CreateMap<Hall, HallDto>();
        CreateMap<Showtime, ShowtimeDto>()
            .ForCtorParam(nameof(ShowtimeDto.MovieTitle), o => o.MapFrom(s => s.Movie.Title))
            .ForCtorParam(nameof(ShowtimeDto.HallName), o => o.MapFrom(s => s.Hall.Name));
    }
}
