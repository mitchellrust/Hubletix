using Microsoft.AspNetCore.Identity;

namespace ClubManagement.Core.Entities;

/// <summary>
/// Application user extending ASP.NET Core Identity User with tenant and role information.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>
    /// Foreign key to Tenant
    /// </summary>
    public Guid TenantId { get; set; }
    
    /// <summary>
    /// User's full name
    /// </summary>
    public string FullName { get; set; } = null!;
    
    /// <summary>
    /// Whether this user account is active
    /// </summary>
    public bool IsActive { get; set; } = true;
    
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
    public ICollection<MembershipSubscription> MembershipSubscriptions { get; set; } = new List<MembershipSubscription>();
    public ICollection<EventSignup> EventSignups { get; set; } = new List<EventSignup>();
    public ICollection<Event> CoachingEvents { get; set; } = new List<Event>();
    public ICollection<PaymentRecord> PaymentRecords { get; set; } = new List<PaymentRecord>();
}
