using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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
    [MaxLength(2000)]
    public string? Description { get; set; }
    
    /// <summary>
    /// Event type (e.g., Class, PersonalTraining, GroupEvent)
    /// </summary>
    [Required]
    public string EventType { get; set; } = Constants.EventType.Other;

    /// <summary>
    /// Foreign key to location where this event takes place
    /// </summary>
    [Required]
    public string LocationId { get; set; } = null!;

    /// <summary>
    /// Optional additional location details (e.g., "Room 101", "Outdoor Field")
    /// </summary>
    [MaxLength(200)]
    public string? LocationDetails { get; set; }
        
    /// <summary>
    /// Maximum capacity for this event
    /// </summary>
    public int Capacity { get; set; } = 15;

    /// <summary>
    /// Price in cents (e.g., 9999 = $99.99)
    /// </summary>
    public int PriceInCents { get; set; }
    
    /// <summary>
    /// Foreign key to TenantUser (coach for this event within tenant context)
    /// </summary>
    public string? CoachId { get; set; }

    /// <summary>
    /// Price formatted to dollar amount (e.g., 99.99)
    /// </summary>
    [NotMapped]
    public decimal PriceInDollars => PriceInCents / 100.0m;
    
    /// <summary>
    /// Event start time (UTC)
    /// </summary>
    public DateTime StartTimeUtc { get; set; }
    
    /// <summary>
    /// Event end time (UTC)
    /// </summary>
    public DateTime EndTimeUtc { get; set; }

    /// <summary>
    /// Registration deadline (UTC, optional)
    /// </summary>
    public DateTime? RegistrationDeadlineUtc { get; set; }
    
    /// <summary>
    /// Time zone ID for the event (e.g., "America/Denver" for Mountain Time)
    /// Uses IANA time zone identifier format.
    /// </summary>
    [Required]
    [StringLength(100)]
    public string TimeZoneId { get; set; } = "America/Denver";
    
    /// <summary>
    /// Whether signups are currently enabled
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public Location Location { get; set; } = null!;
    public TenantUser? Coach { get; set; }
    public ICollection<EventRegistration> EventRegistrations { get; set; } = new List<EventRegistration>();
}
