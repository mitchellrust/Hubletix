using System.ComponentModel.DataAnnotations;
using ClubManagement.Core.Constants;
using ClubManagement.Core.Models;

namespace ClubManagement.Core.Entities;

/// <summary>
/// Represents an event (class, training session, etc.) offered by a tenant.
/// </summary>
public class Event : BaseEntity
{    
    /// <summary>
    /// Event name
    /// </summary>
    public string Name { get; set; } = null!;
    
    /// <summary>
    /// Event description
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Event type (e.g., Class, PersonalTraining, GroupEvent)
    /// </summary>
    [Required]
    public EventType EventType { get; set; } = EventType.Other;
    
    /// <summary>
    /// Foreign key to coach user (optional, can be null for drop-in events)
    /// </summary>
    public string? CoachId { get; set; }
    
    /// <summary>
    /// Maximum capacity for this event
    /// </summary>
    public int Capacity { get; set; } = 15;
    
    /// <summary>
    /// Whether signups are currently enabled
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public User? Coach { get; set; }
    public ICollection<EventRegistration> EventRegistrations { get; set; } = new List<EventRegistration>();
}
