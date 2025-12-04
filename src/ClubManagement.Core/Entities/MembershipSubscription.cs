namespace ClubManagement.Core.Entities;

/// <summary>
/// Represents a membership subscription for a user (their Stripe subscription status).
/// </summary>
public class MembershipSubscription
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Foreign key to ApplicationUser
    /// </summary>
    public Guid UserId { get; set; }
    
    /// <summary>
    /// Foreign key to MembershipPlan
    /// </summary>
    public Guid MembershipPlanId { get; set; }
    
    /// <summary>
    /// Stripe Subscription ID
    /// </summary>
    public string StripeSubscriptionId { get; set; } = null!;
    
    /// <summary>
    /// Whether this subscription is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Subscription start date
    /// </summary>
    public DateTime StartDate { get; set; }
    
    /// <summary>
    /// Subscription end date (null if still active)
    /// </summary>
    public DateTime? EndDate { get; set; }
    
    /// <summary>
    /// Next billing date
    /// </summary>
    public DateTime? NextBillingDate { get; set; }
    
    /// <summary>
    /// Created timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Last updated timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public ApplicationUser User { get; set; } = null!;
    public MembershipPlan MembershipPlan { get; set; } = null!;
}
