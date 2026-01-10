using Hubletix.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Finbuckle.MultiTenant.Abstractions;
using Hubletix.Core.Models;

namespace Hubletix.Api.Filters;

/// <summary>
/// Authorization filter that validates authenticated users have active TenantUser membership
/// for the current tenant (from subdomain) and that their tenant_id claim matches.
/// Redirects to /Tenant/NoAccess if validation fails.
/// </summary>
public class TenantAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly AppDbContext _db;
    private readonly ILogger<TenantAuthorizationFilter> _logger;

    public TenantAuthorizationFilter(AppDbContext db, ILogger<TenantAuthorizationFilter> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        // User should be authenticated at this point (due to [Authorize] attribute)
        if (!user.Identity?.IsAuthenticated ?? true)
        {
            return;
        }

        // Get claims
        var tenantIdClaim = user.FindFirst("tenant_id")?.Value;
        var platformUserIdClaim = user.FindFirst("platform_user_id")?.Value;

        // If no tenant_id claim, user shouldn't be accessing /Tenant routes
        if (string.IsNullOrEmpty(tenantIdClaim) || string.IsNullOrEmpty(platformUserIdClaim))
        {
            _logger.LogWarning("User missing tenant context claims. Platform User ID: {PlatformUserId}, Tenant ID: {TenantId}",
                platformUserIdClaim, tenantIdClaim);
            RedirectToNoAccess(context);
            return;
        }

        // Get current tenant from multi-tenant context
        var tenantContextAccessor = context.HttpContext.RequestServices
            .GetRequiredService<IMultiTenantContextAccessor<ClubTenantInfo>>();
        var tenantInfo = tenantContextAccessor.MultiTenantContext?.TenantInfo;
        
        if (tenantInfo?.Id != tenantIdClaim)
        {
            _logger.LogWarning("User's tenant_id claim {TenantIdClaim} does not match current subdomain tenant {CurrentTenant}",
                tenantIdClaim, tenantInfo?.Id);
            RedirectToNoAccess(context);
            return;
        }

        // Validate user has active TenantUser membership
        var tenantUserExists = await _db.TenantUsers
            .AnyAsync(tu => tu.PlatformUserId == platformUserIdClaim
                && tu.TenantId == tenantIdClaim
                && tu.Status == Core.Enums.TenantUserStatus.Active);

        if (!tenantUserExists)
        {
            _logger.LogWarning("User {PlatformUserId} does not have active TenantUser membership for tenant {TenantId}",
                platformUserIdClaim, tenantIdClaim);
            RedirectToNoAccess(context);
            return;
        }

        // User is authorized
        _logger.LogDebug("User {PlatformUserId} authorized for tenant {TenantId}",
            platformUserIdClaim, tenantIdClaim);
    }

    private static void RedirectToNoAccess(AuthorizationFilterContext context)
    {
        context.Result = new Microsoft.AspNetCore.Mvc.RedirectToPageResult("/Tenant/NoAccess");
    }
}
