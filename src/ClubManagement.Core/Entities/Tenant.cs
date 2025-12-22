using ClubManagement.Core.Models;

namespace ClubManagement.Core.Entities;

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
    /// Whether this tenant is active
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Stripe Connect Account ID for this tenant's payment processing
    /// </summary>
    public string? StripeAccountId { get; set; }
    
    /// <summary>
    /// JSONB stored configuration including theme, feature flags, and settings
    /// </summary>
    public string? ConfigJson { get; set; }
    
    // Navigation properties
    public ICollection<Location> Locations { get; set; } = new List<Location>();
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<MembershipPlan> MembershipPlans { get; set; } = new List<MembershipPlan>();
    public ICollection<Event> Events { get; set; } = new List<Event>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
