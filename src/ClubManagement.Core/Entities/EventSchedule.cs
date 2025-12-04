namespace ClubManagement.Core.Entities;

/// <summary>
/// Represents a specific scheduled occurrence of an event.
/// An Event can have multiple schedules (recurring or one-time classes).
/// </summary>
public class EventSchedule
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Foreign key to Event
    /// </summary>
    public Guid EventId { get; set; }
    
    /// <summary>
    /// Start date and time
    /// </summary>
    public DateTime DateTimeStart { get; set; }
    
    /// <summary>
    /// End date and time
    /// </summary>
    public DateTime DateTimeEnd { get; set; }
    
    /// <summary>
    /// Location or room (optional, e.g., "Studio A" or "Gym Floor")
    /// </summary>
    public string? Location { get; set; }
    
    /// <summary>
    /// Whether this schedule is currently active/bookable
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
    public Event Event { get; set; } = null!;
    public ICollection<EventSignup> Signups { get; set; } = new List<EventSignup>();
}
