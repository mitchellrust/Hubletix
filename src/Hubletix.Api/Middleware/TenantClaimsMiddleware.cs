using Finbuckle.MultiTenant.Abstractions;
using Hubletix.Infrastructure.Persistence;
using Hubletix.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Hubletix.Api.Middleware;

/// <summary>
/// Middleware that enriches the user's claims with tenant-specific information
/// when accessing a tenant subdomain. This ensures that users who logged in at
/// the platform level get the necessary tenant_role and tenant_id claims when
/// they access a tenant's pages.
/// </summary>
public class TenantClaimsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantClaimsMiddleware> _logger;

    public TenantClaimsMiddleware(
        RequestDelegate next,
        ILogger<TenantClaimsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IMultiTenantContextAccessor<ClubTenantInfo> tenantAccessor,
        IClaimsPrincipalFactory claimsFactory,
        UserManager<Core.Entities.User> userManager)
    {
        // Check if we have tenant context
        var tenantInfo = tenantAccessor.MultiTenantContext?.TenantInfo;
        
        if (tenantInfo != null && context.User.Identity?.IsAuthenticated == true)
        {
            // Get current claims
            var currentTenantIdClaim = context.User.FindFirst("tenant_id")?.Value;
            var platformUserIdClaim = context.User.FindFirst("platform_user_id")?.Value;
            var identityUserIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            // Check if user needs tenant claims (either no tenant_id claim, or it doesn't match current tenant)
            if (!string.IsNullOrEmpty(platformUserIdClaim) && 
                !string.IsNullOrEmpty(identityUserIdClaim) &&
                currentTenantIdClaim != tenantInfo.Id)
            {
                _logger.LogInformation(
                    "User {PlatformUserId} accessing tenant {TenantId} without matching tenant claims. Re-authenticating with tenant context.",
                    platformUserIdClaim, tenantInfo.Id);

                try
                {
                    // Get the identity user
                    var identityUser = await userManager.FindByIdAsync(identityUserIdClaim);
                    
                    if (identityUser != null)
                    {
                        // Create new claims principal with tenant context
                        var newPrincipal = await claimsFactory.CreateClaimsPrincipalAsync(
                            identityUser,
                            platformUserIdClaim,
                            tenantInfo.Id);

                        // Get current authentication properties to preserve them
                        var authResult = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
                        var properties = authResult.Properties ?? new AuthenticationProperties();

                        // Re-sign in with new claims
                        await context.SignInAsync(
                            IdentityConstants.ApplicationScheme,
                            newPrincipal,
                            properties);

                        // Update the context user so downstream middleware sees the new claims
                        context.User = newPrincipal;

                        _logger.LogInformation(
                            "Successfully re-authenticated user {PlatformUserId} with tenant {TenantId} claims",
                            platformUserIdClaim, tenantInfo.Id);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Could not find identity user {IdentityUserId} for re-authentication",
                            identityUserIdClaim);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error re-authenticating user {PlatformUserId} with tenant {TenantId}",
                        platformUserIdClaim, tenantInfo.Id);
                    // Continue anyway - authorization will fail if claims are needed
                }
            }
        }

        await _next(context);
    }
}
