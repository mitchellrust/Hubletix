using Hubletix.Core.Models;

namespace Hubletix.Core.Entities;

/// <summary>
/// Represents a signup session tracking a tenant through the onboarding process
/// </summary>
public class SignupSession : BaseEntity
{
    /// <summary>
    /// Foreign key to Tenant (the tenant being created)
    /// Override base property to make it nullable during signup flow
    /// </summary>
    public new string? TenantId { get; set; }

    /// <summary>
    /// Foreign key to PlatformPlan (the plan selected during signup)
    /// </summary>
    public string PlatformPlanId { get; set; } = null!;

    /// <summary>
    /// Foreign key to User (the admin user being created)
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Current state of the signup session
    /// </summary>
    public string State { get; set; } = null!;

    /// <summary>
    /// Email address for the admin user
    /// </summary>
    public string Email { get; set; } = null!;

    /// <summary>
    /// First name for the admin user
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// Last name for the admin user
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// Organization/tenant name
    /// </summary>
    public string? OrganizationName { get; set; }

    /// <summary>
    /// Desired subdomain
    /// </summary>
    public string? Subdomain { get; set; }

    /// <summary>
    /// Stripe Checkout Session ID
    /// </summary>
    public string? StripeCheckoutSessionId { get; set; }

    /// <summary>
    /// When the session expires (24 hours from creation)
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// When the session was completed (if applicable)
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Error message if something went wrong
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Last activity timestamp (for resuming abandoned sessions)
    /// </summary>
    public DateTime LastActivityAt { get; set; }

    // Navigation properties
    public PlatformPlan PlatformPlan { get; set; } = null!;
    public User? User { get; set; }
    public Tenant? Tenant { get; set; }
}
