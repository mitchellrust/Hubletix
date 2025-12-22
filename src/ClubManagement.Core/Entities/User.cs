using System.ComponentModel.DataAnnotations;
using ClubManagement.Core.Models;

namespace ClubManagement.Core.Entities;

/// <summary>
/// User with tenant and role information.
/// </summary>
public class User : BaseEntity
{    
    /// <summary>
    /// User's email address
    /// </summary>
    [Required]
    public string Email { get; set; } = null!;

    /// <summary>
    /// User's first name
    /// </summary>
    public string FirstName { get; set; } = null!;

    /// <summary>
    /// User's last name
    /// </summary>
    public string LastName { get; set; } = null!;
    
    /// <summary>
    /// Whether this user account is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Foreign key to location
    /// </summary>
    [Required]
    public string LocationId { get; set; } = null!;

    /// <summary>
    /// Foreign key to membership plan (if any)
    /// </summary>
    public string? MembershipPlanId { get; set; }
    
    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public Location Location { get; set; } = null!;
    public ICollection<EventRegistration> EventRegistrations { get; set; } = new List<EventRegistration>();
    public ICollection<Event> CoachingEvents { get; set; } = new List<Event>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public MembershipPlan? MembershipPlan { get; set; }
}
