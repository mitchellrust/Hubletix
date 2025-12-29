namespace ClubManagement.Core.Constants;

/// <summary>
/// Platform-level roles for system administration.
/// </summary>
public static class PlatformRoles
{
    /// <summary>
    /// Platform administrator - can manage all tenants and platform configuration
    /// </summary>
    public const string PlatformAdmin = "PlatformAdmin";
    
    /// <summary>
    /// Regular platform user (can create tenants, etc.)
    /// </summary>
    public const string PlatformUser = "PlatformUser";
}
