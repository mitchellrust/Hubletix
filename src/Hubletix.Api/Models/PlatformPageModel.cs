using Microsoft.AspNetCore.Mvc.RazorPages;
using Finbuckle.MultiTenant.Abstractions;
using Hubletix.Infrastructure.Persistence;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Hubletix.Api.Models;

/// <summary>
/// Platform-level page model that operates without tenant context.
/// Used for pages accessible on the root domain that don't require a tenant.
/// </summary>
[AllowAnonymous]
public class PlatformPageModel : PageModel
{
    private readonly IMultiTenantContextAccessor<ClubTenantInfo> _multiTenantContextAccessor;

    public PlatformPageModel(
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor)
    {
        _multiTenantContextAccessor = multiTenantContextAccessor;
    }

    /// <summary>
    /// Gets the current tenant info if available, null otherwise.
    /// Platform pages should not require tenant context.
    /// </summary>
    public ClubTenantInfo? CurrentTenantInfo => 
        _multiTenantContextAccessor.MultiTenantContext?.TenantInfo;

    /// <summary>
    /// Returns true if the page is being accessed in a tenant context (e.g., via subdomain).
    /// </summary>
    public bool HasTenantContext => CurrentTenantInfo != null;

    /// <summary>
    /// Returns true if the current user is authenticated.
    /// </summary>
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    /// <summary>
    /// Gets the platform user ID from claims if authenticated.
    /// </summary>
    public string? PlatformUserId => 
        User?.FindFirst("platform_user_id")?.Value;

    /// <summary>
    /// Gets the user's first name from claims if authenticated.
    /// </summary>
    public string? FirstName => 
        User?.FindFirst("first_name")?.Value;

    /// <summary>
    /// Gets the user's email from claims if authenticated.
    /// </summary>
    public string? Email => 
        User?.FindFirst(ClaimTypes.Email)?.Value;

    /// <summary>
    /// Gets the full name from claims if authenticated.
    /// </summary>
    public string? FullName => 
        User?.FindFirst(ClaimTypes.Name)?.Value;
}
