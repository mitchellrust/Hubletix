using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Hubletix.Core.Constants;
using Hubletix.Core.Models;

namespace Hubletix.Core.Entities;

/// <summary>
/// Represents a membership plan offered by a tenant (e.g., "Monthly CrossFit", "Annual Premium").
/// Pricing and configuration are tenant-specific.
/// </summary>
public class MembershipPlan : BaseEntity
{
    /// <summary>
    /// Plan name (e.g., "Monthly Membership")
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Plan description
    /// </summary>
    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>
    /// Price in cents (e.g., 9999 = $99.99)
    /// </summary>
    public int PriceInCents { get; set; }

    /// <summary>
    /// Price formatted to dollar amount (e.g., 99.99)
    /// </summary>
    [NotMapped]
    public decimal PriceInDollars => PriceInCents / 100.0m;

    /// <summary>
    /// Billing interval: "month" or "year"
    /// </summary>
    public string BillingInterval { get; set; } = BillingIntervals.Monthly;

    /// <summary>
    /// Whether the price is displayed as a monthly equivalent (e.g., $120/year shown as $10/month)
    /// </summary>
    public bool IsPriceDisplayedMonthly { get; set; } = true;

    /// <summary>
    /// Whether this is a recurring subscription plan or a one-time purchase.
    /// </summary>
    [NotMapped]
    public bool IsRecurring => BillingInterval != BillingIntervals.OneTime;

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
    /// Foreign key to location where this membership plan is offered
    /// </summary>
    [Required]
    public string LocationId { get; set; } = null!;

    /// <summary>
    /// Display order (for sorting in UI)
    /// </summary>
    public int DisplayOrder { get; set; } = 0;

    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public Location Location { get; set; } = null!;
}
