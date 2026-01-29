using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Hubletix.Core.Models;

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

    /// <summary>
    /// Homepage content configuration
    /// </summary>
    public HomePageConfig HomePage { get; set; } = new();
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

    /// <summary>
    /// Default country for the tenant (ISO 3166-1 alpha-2 format, e.g., "US")
    /// </summary>
    public string DefaultCountry { get; set; } = "US";
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
    /// Whether user signup is enabled. Users are required for any of the included
    /// features to function.
    /// </summary>
    [NotMapped]
    public bool EnableUserSignup =>
        EnableEventRegistration ||
        EnablePayments ||
        EnableMemberships;

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

    /// <summary>
    /// Enable multiple location support.
    /// </summary>
    public bool EnableMultipleLocations { get; set; } = false;
}

/// <summary>
/// Homepage content configuration
/// </summary>
public class HomePageConfig
{
    /// <summary>
    /// Ordered list of homepage components (max 5)
    /// </summary>
    public List<HomePageComponentConfig> Components { get; set; } = new();
}

/// <summary>
/// Base class for homepage components with discriminator pattern
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(HeroComponentConfig), "Hero")]
[JsonDerivedType(typeof(CardsComponentConfig), "Cards")]
public abstract class HomePageComponentConfig
{
    /// <summary>
    /// Display order of the component
    /// </summary>
    public int Order { get; set; }
}

/// <summary>
/// Hero component configuration (must be first if present)
/// </summary>
public class HeroComponentConfig : HomePageComponentConfig
{
    public string Heading { get; set; } = string.Empty;
    public string Subheading { get; set; } = string.Empty;
    public string? CtaText { get; set; }
    public string? CtaUrl { get; set; }
    public string? BackgroundImageUrl { get; set; }
}

/// <summary>
/// Cards component configuration (reusable up to 3 times)
/// </summary>
public class CardsComponentConfig : HomePageComponentConfig
{
    public string? Heading { get; set; }
    public string? Subheading { get; set; }

    /// <summary>
    /// Up to 3 cards per component
    /// </summary>
    public List<CardConfig> Cards { get; set; } = new();
}

/// <summary>
/// Individual card configuration
/// </summary>
public class CardConfig
{
    public string Heading { get; set; } = string.Empty;
    public string Subheading { get; set; } = string.Empty;
}

/// <summary>
/// Unified render context for homepage components, supporting both Config and ViewModel types.
/// Used for rendering components with theme colors in both preview and production modes.
/// </summary>
/// <typeparam name="T">The component type (HomePageComponentConfig or HomePageComponentViewModel from Api.Models)</typeparam>
public class ComponentRenderContext<T>
{
    public T Component { get; set; } = default!;
    public string PrimaryColor { get; set; } = string.Empty;
    public string SecondaryColor { get; set; } = string.Empty;
    public bool IsPreviewMode { get; set; }
}
