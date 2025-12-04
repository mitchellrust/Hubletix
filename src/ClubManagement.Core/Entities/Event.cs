namespace ClubManagement.Core.Entities;

/// <summary>
/// Represents an event (class, training session, etc.) offered by a tenant.
/// </summary>
public class Event
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Foreign key to Tenant
    /// </summary>
    public Guid TenantId { get; set; }
    
    /// <summary>
    /// Event name
    /// </summary>
    public string Name { get; set; } = null!;
    
    /// <summary>
    /// Event description
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Event type (e.g., "CrossFit Class", "Personal Training", "Group Event")
    /// </summary>
    public string EventType { get; set; } = "Class";
    
    /// <summary>
    /// Foreign key to coach user (optional, can be null for drop-in events)
    /// </summary>
    public Guid? CoachId { get; set; }
    
    /// <summary>
    /// Maximum capacity for this event
    /// </summary>
    public int Capacity { get; set; } = 15;
    
    /// <summary>
    /// Whether signups are currently enabled
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
    public ApplicationUser? Coach { get; set; }
    public ICollection<EventSchedule> Schedules { get; set; } = new List<EventSchedule>();
}
