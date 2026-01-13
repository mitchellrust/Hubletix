using Hubletix.Core.Models;
using Microsoft.Extensions.Hosting;

namespace Hubletix.Infrastructure.Services;

/// <summary>
/// Registry for mapping component types to their partial view paths.
/// Caches mappings in production mode for performance, bypasses cache in development for hot-reload.
/// </summary>
public class HomePageComponentRegistry : IHomePageComponentRegistry
{
    private readonly IHostEnvironment _environment;
    private readonly Dictionary<Type, string> _cache = new();
    private readonly Dictionary<Type, string> _mappings;

    public HomePageComponentRegistry(IHostEnvironment environment)
    {
        _environment = environment;
        
        // Define component type mappings
        _mappings = new Dictionary<Type, string>
        {
            // Config types (used in PageBuilder)
            { typeof(HeroComponentConfig), "_HeroComponent" },
            { typeof(CardsComponentConfig), "_CardsComponent" },
            // Add future component types here
        };
    }

    /// <summary>
    /// Gets the partial view path for a given component type
    /// </summary>
    public string? GetPartialViewPath(Type componentType)
    {
        // In development mode, bypass cache for hot-reload support
        if (_environment.IsDevelopment())
        {
            return GetPartialViewPathInternal(componentType);
        }

        // In production, use cache for performance
        if (_cache.TryGetValue(componentType, out var cachedPath))
        {
            return cachedPath;
        }

        var path = GetPartialViewPathInternal(componentType);
        if (path != null)
        {
            _cache[componentType] = path;
        }

        return path;
    }

    private string? GetPartialViewPathInternal(Type componentType)
    {
        // Direct mapping check
        if (_mappings.TryGetValue(componentType, out var path))
        {
            return path;
        }

        // Check by type name for ViewModel types (which may be in a different assembly)
        var typeName = componentType.Name;
        if (typeName == "HeroComponentViewModel" || typeName == "HeroComponentConfig")
        {
            return "_HeroComponent";
        }
        if (typeName == "CardsComponentViewModel" || typeName == "CardsComponentConfig")
        {
            return "_CardsComponent";
        }

        return null;
    }
}
