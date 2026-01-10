namespace Hubletix.Core.Enums;

/// <summary>
/// Defines the membership status of a user within a specific tenant.
/// Stored as integer in TenantUser.Status column.
/// </summary>
public enum TenantUserStatus
{
    /// <summary>
    /// User is an active member of the tenant
    /// </summary>
    Active = 1,
    
    /// <summary>
    /// User membership is inactive (e.g., expired subscription)
    /// </summary>
    Inactive = 2,
    
    /// <summary>
    /// User has been suspended from the tenant
    /// </summary>
    Suspended = 3,
    
    /// <summary>
    /// User has been invited but not yet accepted
    /// </summary>
    PendingInvite = 4
}
