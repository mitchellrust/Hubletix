using ClubManagement.Core.Models;

namespace ClubManagement.Core.Entities;

/// <summary>
/// Represents a user's signup for a specific event schedule.
/// Enforces unique signup per user/schedule.
/// </summary>
public class EventRegistration: BaseEntity
{    
    /// <summary>
    /// Foreign key to Event
    /// </summary>
    public required string EventId { get; set; }
    
    /// <summary>
    /// Foreign key to PlatformUser
    /// </summary>
    public required string PlatformUserId { get; set; }
    
    /// <summary>
    /// Signup status: "registered", "cancelled", "attended"
    /// </summary>
    public string Status { get; set; } = Constants.EventRegistrationStatus.Registered;
    
    /// <summary>
    /// Cancellation reason (if cancelled)
    /// </summary>
    public string? CancellationReason { get; set; }
    
    /// <summary>
    /// Signup timestamp
    /// </summary>
    public DateTime SignedUpAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public PlatformUser PlatformUser { get; set; } = null!;
    public Event Event { get; set; } = null!;
}
