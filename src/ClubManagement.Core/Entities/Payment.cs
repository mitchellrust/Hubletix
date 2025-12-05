using ClubManagement.Core.Models;

namespace ClubManagement.Core.Entities;

/// <summary>
/// Represents a payment record (Stripe charge or subscription payment).
/// </summary>
public class Payment : BaseEntity
{    
    /// <summary>
    /// Foreign key to User (can be null for administrative payments)
    /// </summary>
    public string? UserId { get; set; }
    
    /// <summary>
    /// Stripe Payment Intent ID or Charge ID
    /// </summary>
    public string StripePaymentId { get; set; } = null!;
    
    /// <summary>
    /// Amount in cents
    /// </summary>
    public int AmountInCents { get; set; }
    
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
    public User? User { get; set; }
}
