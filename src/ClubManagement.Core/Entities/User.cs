using Microsoft.AspNetCore.Identity;
using ClubManagement.Core.Constants;

namespace ClubManagement.Core.Entities;

/// <summary>
/// User with tenant and role information, extends IdentityUser for authentication.
/// </summary>
public class User : IdentityUser
{    
    /// <summary>
    /// Foreign key to Tenant
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Unique identifier for the entity (alias for Id from IdentityUser)
    /// </summary>
    public string EntityId => Id;

    /// <summary>
    /// User's first name
    /// </summary>
    public string FirstName { get; set; } = null!;

    /// <summary>
    /// User's last name
    /// </summary>
    public string LastName { get; set; } = null!;
    
    /// <summary>
    /// User's role: Admin, Coach, Member
    /// </summary>
    public string Role { get; set; } = UserRoles.Member;
    
    /// <summary>
    /// Whether this user account is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Foreign key to location
    /// </summary>
    public string? LocationId { get; set; }

    /// <summary>
    /// Foreign key to membership plan (if any)
    /// </summary>
    public string? MembershipPlanId { get; set; }
    
    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public Location? Location { get; set; }
    public ICollection<EventRegistration> EventRegistrations { get; set; } = new List<EventRegistration>();
    public ICollection<Event> CoachingEvents { get; set; } = new List<Event>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public MembershipPlan? MembershipPlan { get; set; }
}
