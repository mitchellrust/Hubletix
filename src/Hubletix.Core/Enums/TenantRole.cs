namespace Hubletix.Core.Enums;

/// <summary>
/// Defines the role a user has within a specific tenant.
/// Stored as integer in TenantUser.Role column.
/// </summary>
public enum TenantRole
{
    /// <summary>
    /// Regular member with basic access to tenant resources
    /// </summary>
    Member = 1,

    /// <summary>
    /// Coach with ability to manage events and sessions
    /// </summary>
    Coach = 2,

    /// <summary>
    /// Administrator with full control over tenant configuration
    /// </summary>
    Admin = 3
}
