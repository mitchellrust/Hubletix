namespace ClubManagement.Core.Entities;

/// <summary>
/// Maps users to roles within specific tenants.
/// Allows a user to have different roles in different tenants.
/// </summary>
public class TenantUserRole
{
    public int Id { get; set; }
    
    /// <summary>
    /// The tenant this role applies to
    /// </summary>
    public string TenantId { get; set; } = default!;
    
    /// <summary>
    /// The user this role is assigned to
    /// </summary>
    public string UserId { get; set; } = default!;
    
    /// <summary>
    /// The role name (Admin, Member, Coach, etc.)
    /// </summary>
    public string Role { get; set; } = default!;
    
    /// <summary>
    /// When this role was assigned
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Who assigned this role
    /// </summary>
    public string? CreatedBy { get; set; }
    
    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public User User { get; set; } = null!;
}
