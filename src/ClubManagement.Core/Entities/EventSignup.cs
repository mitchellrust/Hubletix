namespace ClubManagement.Core.Entities;

/// <summary>
/// Represents a user's signup for a specific event schedule.
/// Enforces unique signup per user/schedule.
/// </summary>
public class EventSignup
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Foreign key to EventSchedule
    /// </summary>
    public Guid ScheduleId { get; set; }
    
    /// <summary>
    /// Foreign key to ApplicationUser
    /// </summary>
    public Guid UserId { get; set; }
    
    /// <summary>
    /// Signup status: "registered", "cancelled", "attended"
    /// </summary>
    public string Status { get; set; } = "registered";
    
    /// <summary>
    /// Cancellation reason (if cancelled)
    /// </summary>
    public string? CancellationReason { get; set; }
    
    /// <summary>
    /// Signup timestamp
    /// </summary>
    public DateTime SignedUpAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Last updated timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public EventSchedule Schedule { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
