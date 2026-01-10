using ClubManagement.Core.Enums;
using ClubManagement.Core.Models;

namespace ClubManagement.Core.Entities;

/// <summary>
/// Join entity representing a platform user's membership in a specific tenant.
/// This is where tenant-scoped roles are stored.
/// One user can belong to multiple tenants with different roles.
/// </summary>
public class TenantUser : BaseEntity
{
    /// <summary>
    /// Foreign key to Tenant (uses BaseEntity.TenantId)
    /// </summary>
    // TenantId inherited from BaseEntity
    
    /// <summary>
    /// Foreign key to PlatformUser
    /// </summary>
    public required string PlatformUserId { get; set; }
    
    /// <summary>
    /// The role this user has within this specific tenant
    /// </summary>
    public TenantRole Role { get; set; }
    
    /// <summary>
    /// The membership status within this tenant
    /// </summary>
    public TenantUserStatus Status { get; set; }
    
    /// <summary>
    /// Whether this user is the owner/creator of the tenant
    /// </summary>
    public bool IsOwner { get; set; }
    
    // CreatedAt and CreatedBy inherited from BaseEntity
    
    // Navigation Properties
    
    /// <summary>
    /// Navigation to the tenant
    /// </summary>
    public Tenant Tenant { get; set; } = null!;
    
    /// <summary>
    /// Navigation to the platform user
    /// </summary>
    public PlatformUser PlatformUser { get; set; } = null!;
    
    /// <summary>
    /// Events coached by this user in this tenant
    /// </summary>
    public ICollection<Event> CoachedEvents { get; set; } = new List<Event>();
}
