using Catalog.Bll.Caching;
using Catalog.Bll.Interfaces;
using Catalog.Bll.Mapping;
using Catalog.Bll.Services;
using Catalog.Bll.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog.Bll;

public static class DependencyInjection
{
    /// <summary>Сервіси BLL (Scoped), AutoMapper-профілі, валідатори FluentValidation, кеш довідників.</summary>
    public static IServiceCollection AddCatalogBll(this IServiceCollection services, string? autoMapperLicenseKey)
    {
        services.AddScoped<IMovieService, MovieService>();
        services.AddScoped<IGenreService, GenreService>();
        services.AddScoped<IShowtimeService, ShowtimeService>();

        services.AddMemoryCache();
        services.AddSingleton<CatalogCache>();
        services.AddSingleton(TimeProvider.System);

        // усі профілі BLL — централізовано однією збіркою
        services.AddAutoMapper(cfg => cfg.LicenseKey = autoMapperLicenseKey, typeof(MovieProfile).Assembly);
        // усі валідатори — окремі класи, реєструються за збіркою, викликаються явно в контролерах
        services.AddValidatorsFromAssemblyContaining<CreateMovieDtoValidator>();
        return services;
    }
}
