namespace Catalog.Api.Metrics;

internal static class ServiceCollectionDecorateExtensions
{
    /// <summary>Замінює реєстрацію TService декоратором, зберігаючи її lifetime (без сторонніх бібліотек).</summary>
    public static IServiceCollection Decorate<TService>(this IServiceCollection services,
                                                        Func<TService, IServiceProvider, TService> decorate) where TService : class
    {
        var descriptor = services.Last(d => d.ServiceType == typeof(TService));
        services.Remove(descriptor);
        services.Add(new ServiceDescriptor(typeof(TService), sp =>
        {
            var inner = descriptor.ImplementationFactory is not null
                ? (TService)descriptor.ImplementationFactory(sp)
                : descriptor.ImplementationInstance is not null
                    ? (TService)descriptor.ImplementationInstance
                    : (TService)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!);
            return decorate(inner, sp);
        }, descriptor.Lifetime));
        return services;
    }
}
