using Microsoft.AspNetCore.Identity;

namespace Hubletix.Core.Entities;

/// <summary>
/// Identity user for authentication (ASP.NET Core Identity).
/// Has a 1:1 relationship with PlatformUser for domain-level user data.
/// Keep this class focused ONLY on authentication concerns.
/// </summary>
public class User : IdentityUser
{
    /// <summary>
    /// Navigation to the domain-level platform user
    /// </summary>
    public PlatformUser? PlatformUser { get; set; }
}
