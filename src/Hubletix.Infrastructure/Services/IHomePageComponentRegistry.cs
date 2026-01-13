namespace Hubletix.Infrastructure.Services;

/// <summary>
/// Registry for mapping component types to their partial view paths
/// </summary>
public interface IHomePageComponentRegistry
{
    /// <summary>
    /// Gets the partial view path for a given component type
    /// </summary>
    /// <param name="componentType">The type of the component</param>
    /// <returns>The partial view path, or null if not found</returns>
    string? GetPartialViewPath(Type componentType);
}
