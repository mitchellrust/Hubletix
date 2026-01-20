using Hubletix.Core.Models;

namespace Hubletix.Core.Entities;

/// <summary>
/// Represents a tenant (club) in the system.
/// Each tenant has their own isolated data within the shared database.
/// </summary>
public class Tenant : BaseEntity
{    
    /// <summary>
    /// Tenant name (e.g., "Downtown CrossFit")
    /// </summary>
    public string Name { get; set; } = null!;
    
    /// <summary>
    /// Subdomain for tenant routing (e.g., "downtown-crossfit" from "downtown-crossfit.mydomain.com")
    /// </summary>
    public string Subdomain { get; set; } = null!;
    
    /// <summary>
    /// Tenant status: PendingActivation, Active, Suspended, Cancelled
    /// </summary>
    public string Status { get; set; } = Constants.TenantStatus.PendingActivation;
    
    /// <summary>
    /// Stripe Connect Account ID for this tenant's payment processing
    /// </summary>
    public string? StripeAccountId { get; set; }

    /// <summary>
    /// Latest requirements status from Stripe (e.g., "currently_due", "past_due", "complete")
    /// </summary>
    public string StripeAccountRequirementsStatus { get; set; } = Constants.StripeAccountRequirementsStatus.None;
    
    /// <summary>
    /// Stripe Connect onboarding state: NotStarted, AccountCreated, OnboardingStarted, Completed
    /// </summary>
    public string StripeOnboardingState { get; set; } = Constants.StripeOnboardingState.NotStarted;
    
    /// <summary>
    /// Whether the Stripe Connect account has charges enabled
    /// </summary>
    public bool ChargesEnabled { get; set; }
    
    /// <summary>
    /// Whether the Stripe Connect account has payouts enabled
    /// </summary>
    public bool PayoutsEnabled { get; set; }
    
    /// <summary>
    /// Whether all required details have been submitted to Stripe
    /// </summary>
    public bool DetailsSubmitted { get; set; }
    
    /// <summary>
    /// Timestamp when Stripe onboarding was completed (charges enabled)
    /// </summary>
    public DateTime? OnboardingCompletedAt { get; set; }
    
    /// <summary>
    /// JSONB stored configuration including theme, feature flags, and settings
    /// </summary>
    public string? ConfigJson { get; set; }
    
    // Navigation properties
    public ICollection<Location> Locations { get; set; } = new List<Location>();
    public ICollection<TenantUser> TenantUsers { get; set; } = new List<TenantUser>();
    public ICollection<MembershipPlan> MembershipPlans { get; set; } = new List<MembershipPlan>();
    public ICollection<Event> Events { get; set; } = new List<Event>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<TenantSubscription> Subscriptions { get; set; } = new List<TenantSubscription>();
}
