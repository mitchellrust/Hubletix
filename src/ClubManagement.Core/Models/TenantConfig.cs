namespace ClubManagement.Core.Models;

/// <summary>
/// Represents the configuration stored in Tenant.ConfigJson.
/// This is serialized/deserialized as JSON and stored in the database.
/// </summary>
public class TenantConfig
{
    /// <summary>
    /// General settings for the tenant
    /// </summary>
    public SettingsConfig Settings { get; set; } = new();

    /// <summary>
    /// Theme configuration
    /// </summary>
    public ThemeConfig Theme { get; set; } = new();

    /// <summary>
    /// Feature flags for enabling/disabling functionality
    /// </summary>
    public FeatureFlags Features { get; set; } = new();
}

public class SettingsConfig
{
    /// <summary>
    /// Default time zone for the tenant (IANA format, e.g., "America/Denver")
    /// </summary>
    public string TimeZoneId { get; set; } = "America/Denver";

    /// <summary>
    /// Default currency for payments (e.g., "usd")
    /// </summary>
    public string DefaultCurrency { get; set; } = "usd";
}

/// <summary>
/// Theme customization settings
/// </summary>
public class ThemeConfig
{
    /// <summary>
    /// Primary brand color (hex format, e.g., "#4F46E5")
    /// </summary>
    public string? PrimaryColor { get; set; }

    /// <summary>
    /// Secondary brand color (hex format, e.g., "#06B6D4")
    /// </summary>
    public string? SecondaryColor { get; set; }

    /// <summary>
    /// Logo URL or path
    /// </summary>
    public string? LogoUrl { get; set; }
}

/// <summary>
/// Feature flags for tenant-specific functionality
/// </summary>
public class FeatureFlags
{
    /// <summary>
    /// Enable event registration functionality
    /// </summary>
    public bool EnableEventRegistration { get; set; } = true;

    /// <summary>
    /// Enable payment processing
    /// </summary>
    public bool EnablePayments { get; set; } = true;

    /// <summary>
    /// Enable membership plans
    /// </summary>
    public bool EnableMemberships { get; set; } = true;
}
