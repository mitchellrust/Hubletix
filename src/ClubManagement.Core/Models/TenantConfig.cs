using System.ComponentModel.DataAnnotations.Schema;

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
    public bool EnableEventRegistration { get; set; } = false;

    /// <summary>
    /// Enable payment processing
    /// </summary>
    public bool EnablePayments { get; set; } = false;

    /// <summary>
    /// Enable membership plans
    /// </summary>
    public bool EnableMemberships { get; set; } = false;
}

/// <summary>
/// Homepage content configuration
/// </summary>
public class HomePageConfig
{
    /// <summary>
    /// Hero section configuration
    /// </summary>
    public HeroConfig? Hero { get; set; }

    /// <summary>
    /// About section configuration
    /// </summary>
    public AboutConfig? About { get; set; }

    /// <summary>
    /// Services section configuration
    /// </summary>
    public ServicesConfig? Services { get; set; }

    /// <summary>
    /// Section visibility controls
    /// </summary>
    public SectionVisibility Visibility { get; set; } = new();
}

public class HeroConfig
{
    public string? ImageUrl { get; set; }
    public string Heading { get; set; } = string.Empty;
    public string Subheading { get; set; } = string.Empty;
    public string? CtaText { get; set; }
    public string? CtaUrl { get; set; }
}

public class AboutConfig
{
    public string Heading { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// About section feature cards
    /// </summary>
    public List<FeatureCardConfig>? FeatureCards { get; set; }
}

public class FeatureCardConfig
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? Icon { get; set; }
    public int DisplayOrder { get; set; }
}

public class ServicesConfig
{
    public string Heading { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Services section service cards
    /// </summary>
    public List<ServiceCardConfig>? ServiceCards { get; set; }
}

public class ServiceCardConfig
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? Icon { get; set; }
    public string? LinkUrl { get; set; }
    public string? LinkText { get; set; }
    public int DisplayOrder { get; set; }
}

public class SectionVisibility
{
    public bool ShowHero { get; set; } = true;
    public bool ShowAbout { get; set; } = true;
    public bool ShowServices { get; set; } = true;
}
