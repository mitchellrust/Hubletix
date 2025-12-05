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
    public string UserName { get; set; } = null!;

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
    
    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public ICollection<EventRegistration> EventRegistrations { get; set; } = new List<EventRegistration>();
    public ICollection<Event> CoachingEvents { get; set; } = new List<Event>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
