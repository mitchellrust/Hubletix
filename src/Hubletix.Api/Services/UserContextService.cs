using System.Security.Claims;

namespace Hubletix.Api.Services;

/// <summary>
/// Provides typed access to user claims from the current request context.
/// Claims are populated from the authentication cookie.
/// </summary>
public interface IUserContextService
{
    /// <summary>
    /// Gets the current authenticated user's platform user ID.
    /// </summary>
    string? PlatformUserId { get; }

    /// <summary>
    /// Gets the current authenticated user's tenant ID if applicable.
    /// Returns null if user is not in a tenant context.
    /// </summary>
    string? TenantId { get; }

    /// <summary>
    /// Gets the current authenticated user's role within the current tenant.
    /// Returns null if user is not in a tenant context.
    /// </summary>
    string? TenantRole { get; }

    /// <summary>
    /// Gets whether the current user is the owner of the current tenant.
    /// </summary>
    bool IsOwner { get; }

    /// <summary>
    /// Gets the current authenticated user's first name.
    /// </summary>
    string? FirstName { get; }

    /// <summary>
    /// Gets whether the user is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the current user's platform-level role (e.g., "PlatformAdmin").
    /// </summary>
    string? PlatformRole { get; }
}

public class UserContextService : IUserContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserContextService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string? PlatformUserId => User?.FindFirst("platform_user_id")?.Value;

    public string? TenantId => User?.FindFirst("tenant_id")?.Value;

    public string? TenantRole => User?.FindFirst("tenant_role")?.Value;

    public bool IsOwner
    {
        get
        {
            var ownerClaim = User?.FindFirst("is_tenant_owner")?.Value;
            return ownerClaim == "true";
        }
    }

    public string? FirstName => User?.FindFirst("first_name")?.Value;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public string? PlatformRole => User?.FindFirst("platform_role")?.Value;
}
