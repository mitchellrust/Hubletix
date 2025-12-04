namespace ClubManagement.Core.Entities;

/// <summary>
/// Represents a membership plan offered by a tenant (e.g., "Monthly CrossFit", "Annual Premium").
/// Pricing and configuration are tenant-specific.
/// </summary>
public class MembershipPlan
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Foreign key to Tenant
    /// </summary>
    public Guid TenantId { get; set; }
    
    /// <summary>
    /// Plan name (e.g., "Monthly Membership")
    /// </summary>
    public string Name { get; set; } = null!;
    
    /// <summary>
    /// Plan description
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Price in cents (e.g., 9999 = $99.99)
    /// </summary>
    public int PriceInCents { get; set; }
    
    /// <summary>
    /// Billing interval: "month" or "year"
    /// </summary>
    public string BillingInterval { get; set; } = "month";
    
    /// <summary>
    /// Stripe Product ID for this plan
    /// </summary>
    public string? StripeProductId { get; set; }
    
    /// <summary>
    /// Stripe Price ID for this plan
    /// </summary>
    public string? StripePriceId { get; set; }
    
    /// <summary>
    /// Whether this plan is currently available for purchase
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Display order (for sorting in UI)
    /// </summary>
    public int DisplayOrder { get; set; } = 0;
    
    /// <summary>
    /// Created timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Last updated timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public ICollection<MembershipSubscription> Subscriptions { get; set; } = new List<MembershipSubscription>();
}
