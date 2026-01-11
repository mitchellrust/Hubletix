using Hubletix.Core.Models;

namespace Hubletix.Core.Entities;

/// <summary>
/// Represents a platform user - a real person authenticated via Identity.
/// Has a 1:1 relationship with IdentityUser (AspNetUsers).
/// This is the domain model for users, separated from authentication concerns.
/// </summary>
public class PlatformUser : BaseEntity
{
    /// <summary>
    /// Foreign key to AspNetUsers.Id (IdentityUser)
    /// </summary>
    public required string IdentityUserId { get; set; }
    
    public required string FirstName { get; set; }
    
    public required string LastName { get; set; }
    
    /// <summary>
    /// Whether the user account is active
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    // Navigation Properties
    
    /// <summary>
    /// Navigation to the Identity user (authentication layer)
    /// </summary>
    public User IdentityUser { get; set; } = null!;
        
    /// <summary>
    /// All tenant memberships for this user
    /// </summary>
    public ICollection<TenantUser> TenantMemberships { get; set; } = new List<TenantUser>();
    
    /// <summary>
    /// Event registrations made by this user
    /// </summary>
    public ICollection<EventRegistration> EventRegistrations { get; set; } = new List<EventRegistration>();
    
    /// <summary>
    /// Payments made by this user
    /// </summary>
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    
    /// <summary>
    /// Full name display
    /// </summary>
    public string FullName => $"{FirstName} {LastName}";
}
