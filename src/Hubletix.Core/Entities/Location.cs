using System.ComponentModel.DataAnnotations;
using Hubletix.Core.Models;

namespace Hubletix.Core.Entities;

/// <summary>
/// Represents a physical location for a tenant (club).
/// Multi-location tenants can have multiple locations, each with their own operational data.
/// Financial reporting and payments roll up to the tenant level.
/// </summary>
public class Location : BaseEntity
{
    /// <summary>
    /// Location name (e.g., "Downtown Branch", "North Campus")
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Physical address of the location
    /// </summary>
    [MaxLength(500)]
    public string? Address { get; set; }

    /// <summary>
    /// City
    /// </summary>
    [MaxLength(100)]
    public string? City { get; set; }

    /// <summary>
    /// State or province
    /// </summary>
    [MaxLength(100)]
    public string? State { get; set; }

    /// <summary>
    /// Postal code
    /// </summary>
    [MaxLength(20)]
    public string? PostalCode { get; set; }

    /// <summary>
    /// Country
    /// </summary>
    [MaxLength(100)]
    public string? Country { get; set; }

    /// <summary>
    /// Phone number for this location
    /// </summary>
    [MaxLength(50)]
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Email for this location
    /// </summary>
    [MaxLength(255)]
    public string? Email { get; set; }

    /// <summary>
    /// Time zone ID for the location (e.g., "America/Denver" for Mountain Time)
    /// Uses IANA time zone identifier format.
    /// </summary>
    [Required]
    [StringLength(100)]
    public string TimeZoneId { get; set; } = "America/Denver";

    /// <summary>
    /// Whether this is the default location for the tenant.
    /// Single-location tenants will have one default location.
    /// </summary>
    public bool IsDefault { get; set; } = false;

    /// <summary>
    /// Whether this location is active and available for operations
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public ICollection<Event> Events { get; set; } = new List<Event>();
    public ICollection<MembershipPlan> MembershipPlans { get; set; } = new List<MembershipPlan>();
}
