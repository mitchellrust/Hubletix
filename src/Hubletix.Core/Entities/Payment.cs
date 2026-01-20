using System.ComponentModel.DataAnnotations.Schema;
using Hubletix.Core.Models;

namespace Hubletix.Core.Entities;

/// <summary>
/// Represents a payment record (Stripe charge or subscription payment).
/// </summary>
public class Payment : BaseEntity
{    
    /// <summary>
    /// Foreign key to PlatformUser (can be null for administrative payments)
    /// </summary>
    public string? PlatformUserId { get; set; }
    
    /// <summary>
    /// Stripe Payment Intent ID or Charge ID
    /// </summary>
    public string StripePaymentId { get; set; } = null!;
    
    /// <summary>
    /// Price in cents
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
    /// Payment status: "pending", "succeeded", "failed", "refunded"
    /// </summary>
    public string Status { get; set; } = "pending";
    
    /// <summary>
    /// Payment type: "subscription", "one_time", "refund"
    /// </summary>
    public string PaymentType { get; set; } = "subscription";
    
    /// <summary>
    /// Description of what was paid for
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Timestamp of the payment
    /// </summary>
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public PlatformUser? PlatformUser { get; set; }
}
