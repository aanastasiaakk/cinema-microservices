using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reviews.Application.Caching;
using Reviews.Application.Common.Behaviors;
using Reviews.Application.Mapping;

namespace Reviews.Application;

public static class DependencyInjection
{
    /// <summary>
    /// MediatR (сканування обробників збірки) + pipeline behaviors у фіксованому порядку,
    /// валідатори FluentValidation, AutoMapper-профілі, кеш. Порядок AddOpenBehavior = порядок виконання:
    /// Logging (зовнішній) → Validation → Caching → Performance (біля обробника) → Handler.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration config)
    {
        var assembly = typeof(DependencyInjection).Assembly;
        var licenseKey = config["LuckyPenny:LicenseKey"];

        services.AddMediatR(cfg =>
        {
            if (!string.IsNullOrWhiteSpace(licenseKey)) cfg.LicenseKey = licenseKey;
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));       // 1 — зовнішній
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));    // 2 — помилки до обробника
            cfg.AddOpenBehavior(typeof(CachingBehavior<,>));       // 3 — лише ICacheableQuery
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));   // 4 — біля обробника
        });
        services.AddValidatorsFromAssembly(assembly);
        services.AddAutoMapper(cfg =>
        {
            if (!string.IsNullOrWhiteSpace(licenseKey)) cfg.LicenseKey = licenseKey;
        }, typeof(ReviewProfile).Assembly);

        services.AddMemoryCache();
        services.AddSingleton<ICacheInvalidator, MemoryCacheInvalidator>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
