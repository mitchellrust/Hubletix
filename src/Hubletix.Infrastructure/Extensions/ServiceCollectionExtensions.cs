using Hubletix.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Hubletix.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering homepage-related services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers homepage-related services to the DI container
    /// </summary>
    public static IServiceCollection AddHomePageServices(this IServiceCollection services)
    {
        services.AddSingleton<IHomePageComponentRegistry, HomePageComponentRegistry>();
        
        return services;
    }
}
