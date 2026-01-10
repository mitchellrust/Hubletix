using System.Text.Json;
using Hubletix.Core.Entities;
using Hubletix.Core.Models;

namespace Hubletix.Api.Utils;

/// <summary>
/// Extension methods for working with Tenant configuration.
/// </summary>
public static class TenantConfigExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Deserializes the Tenant.ConfigJson property into a strongly-typed TenantConfig object.
    /// Returns a default TenantConfig if ConfigJson is null or invalid.
    /// </summary>
    /// <param name="tenant">The tenant entity.</param>
    /// <returns>A TenantConfig object.</returns>
    public static TenantConfig GetConfig(this Tenant tenant)
    {
        if (string.IsNullOrWhiteSpace(tenant.ConfigJson))
        {
            return new TenantConfig();
        }

        try
        {
            return JsonSerializer.Deserialize<TenantConfig>(tenant.ConfigJson, JsonOptions) 
                   ?? new TenantConfig();
        }
        catch
        {
            // If deserialization fails, return default config
            return new TenantConfig();
        }
    }

    /// <summary>
    /// Serializes a TenantConfig object and sets it to the Tenant.ConfigJson property.
    /// </summary>
    /// <param name="tenant">The tenant entity.</param>
    /// <param name="config">The configuration to serialize.</param>
    public static void SetConfig(this Tenant tenant, TenantConfig config)
    {
        tenant.ConfigJson = JsonSerializer.Serialize(config, JsonOptions);
    }

    /// <summary>
    /// Updates the Tenant.ConfigJson property with a modified configuration.
    /// Provides a callback to modify the existing config.
    /// </summary>
    /// <param name="tenant">The tenant entity.</param>
    /// <param name="updateAction">Action to modify the configuration.</param>
    public static void UpdateConfig(this Tenant tenant, Action<TenantConfig> updateAction)
    {
        var config = tenant.GetConfig();
        updateAction(config);
        tenant.SetConfig(config);
    }
}
