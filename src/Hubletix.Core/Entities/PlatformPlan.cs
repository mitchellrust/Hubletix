using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Hubletix.Core.Constants;
using Hubletix.Core.Models;

namespace Hubletix.Core.Entities;

/// <summary>
/// Represents a platform subscription plan offered to tenants
/// (e.g., "Starter", "Professional", "Enterprise")
/// </summary>
public class PlatformPlan : BaseEntity
{
    /// <summary>
    /// Plan name (e.g., "Starter Plan")
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Plan description
    /// </summary>
    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>
    /// Price in cents (e.g., 2900 for $29.00)
    /// </summary>
    public int PriceInCents { get; set; }

    /// <summary>
    /// Price formatted to dollar amount (e.g., 99.99)
    /// </summary>
    [NotMapped]
    public decimal PriceInDollars => PriceInCents / 100.0m;

    /// <summary>
    /// Currency code (e.g., "usd")
    /// </summary>
    public string Currency { get; set; } = "usd";

    /// <summary>
    /// Billing interval: "month" or "year"
    /// </summary>
    public string BillingInterval { get; set; } = BillingIntervals.Monthly;

    /// <summary>
    /// Whether this is a recurring subscription plan or a one-time purchase.
    /// </summary>
    [NotMapped]
    public bool IsRecurring => BillingInterval != BillingIntervals.OneTime;

    /// <summary>
    /// Stripe Product ID for this platform plan
    /// </summary>
    public string? StripeProductId { get; set; }

    /// <summary>
    /// Stripe Price ID for this platform plan
    /// </summary>
    public string? StripePriceId { get; set; }

    /// <summary>
    /// Whether this plan is currently available for purchase
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether this plan is featured/recommended
    /// </summary>
    public bool IsFeatured { get; set; } = false;

    /// <summary>
    /// Display order for sorting
    /// </summary>
    public int DisplayOrder { get; set; } = 0;

    // Navigation properties
    public ICollection<TenantSubscription> TenantSubscriptions { get; set; } = new List<TenantSubscription>();
}
