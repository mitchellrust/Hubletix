namespace Hubletix.Core.Models;

/// <summary>
/// Configuration settings for Stripe integration.
/// Binds to "Stripe" section in appsettings.json.
/// </summary>
public class StripeSettings
{
    /// <summary>
    /// Platform Stripe configuration (for collecting payments from tenants to the platform)
    /// </summary>
    public StripePlatformSettings Platform { get; set; } = new();

    /// <summary>
    /// Stripe Connect configuration (for tenant payment processing via Connect accounts)
    /// </summary>
    public StripeConnectSettings Connect { get; set; } = new();
}

/// <summary>
/// Platform Stripe settings for direct payments to the platform.
/// Used when collecting fees from tenants.
/// </summary>
public class StripePlatformSettings
{
    /// <summary>
    /// Stripe secret key for platform account
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Stripe publishable key for platform account
    /// </summary>
    public string PublishableKey { get; set; } = string.Empty;

    /// <summary>
    /// Webhook secret for verifying platform webhook signatures
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;
}

/// <summary>
/// Stripe Connect settings for tenant payment processing.
/// Used when tenants collect payments from their members.
/// </summary>
public class StripeConnectSettings
{
    /// <summary>
    /// Platform Stripe secret key (used to create and manage Connect accounts)
    /// </summary>
    public string PlatformSecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Platform Stripe publishable key
    /// </summary>
    public string PlatformPublishableKey { get; set; } = string.Empty;

    /// <summary>
    /// Webhook secret for verifying Connect webhook signatures
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>
    /// Platform application fee percentage (e.g., 0.10 for 10%)
    /// </summary>
    public decimal ApplicationFeePercent { get; set; } = 0.0m;

    /// <summary>
    /// OAuth Client ID for Stripe Connect (if using OAuth flow)
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Redirect URL after successful Connect onboarding
    /// </summary>
    public string? OnboardingSuccessUrl { get; set; }

    /// <summary>
    /// Redirect URL if Connect onboarding fails or is cancelled
    /// </summary>
    public string? OnboardingFailureUrl { get; set; }
}
