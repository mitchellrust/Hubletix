using ClubManagement.Core.Constants;
using ClubManagement.Core.Models;

namespace ClubManagement.Core.Entities;

/// <summary>
/// Represents a tenant's subscription to a platform plan
/// </summary>
public class TenantSubscription : BaseEntity
{   
    /// <summary>
    /// Foreign key to PlatformPlan
    /// </summary>
    public string PlatformPlanId { get; set; } = null!;
    
    /// <summary>
    /// Stripe Customer ID for the tenant
    /// </summary>
    public string? StripeCustomerId { get; set; }
    
    /// <summary>
    /// Stripe Subscription ID
    /// </summary>
    public string? StripeSubscriptionId { get; set; }
    
    /// <summary>
    /// Subscription status: "active", "past_due", "canceled", "unpaid", "incomplete"
    /// </summary>
    public string Status { get; set; } = SubscriptionStatus.Incomplete;
    
    /// <summary>
    /// Current billing period start date
    /// </summary>
    public DateTime? CurrentPeriodStart { get; set; }
    
    /// <summary>
    /// Current billing period end date
    /// </summary>
    public DateTime? CurrentPeriodEnd { get; set; }
    
    /// <summary>
    /// Date when the subscription was cancelled (if applicable)
    /// </summary>
    public DateTime? CancelledAt { get; set; }
    
    /// <summary>
    /// Date when the subscription ends after cancellation
    /// </summary>
    public DateTime? EndsAt { get; set; }
    
    /// <summary>
    /// Whether the subscription will renew at the end of the period
    /// </summary>
    public bool WillRenew { get; set; } = true;
    
    /// <summary>
    /// Trial end date (if applicable)
    /// </summary>
    public DateTime? TrialEndsAt { get; set; }
    
    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public PlatformPlan PlatformPlan { get; set; } = null!;
}
