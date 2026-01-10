using Hubletix.Core.Enums;
using Hubletix.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Hubletix.Api.Controllers;

/// <summary>
/// Controller for handling tenant switching operations.
/// When a user switches to a different tenant, their authentication cookie
/// is updated with new tenant claims.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SwitchTenantController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<SwitchTenantController> _logger;

    public SwitchTenantController(AppDbContext db, ILogger<SwitchTenantController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Switches the authenticated user to a different tenant and refreshes their authentication claims.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant to switch to</param>
    /// <returns>Redirects to the tenant home page or returns error</returns>
    [HttpPost("switch/{tenantId}")]
    public async Task<IActionResult> SwitchTenant(string tenantId)
    {
        var platformUserIdClaim = User.FindFirst("platform_user_id")?.Value;

        if (string.IsNullOrEmpty(platformUserIdClaim))
        {
            _logger.LogWarning("SwitchTenant called by unauthenticated user");
            return Unauthorized("You must be logged in to switch tenants.");
        }

        // Validate user has access to target tenant
        var tenantUsership = await _db.TenantUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(tu => tu.PlatformUserId == platformUserIdClaim
                && tu.TenantId == tenantId
                && tu.Status == TenantUserStatus.Active);

        if (tenantUsership == null)
        {
            _logger.LogWarning("User {PlatformUserId} attempted to switch to unauthorized tenant {TenantId}",
                platformUserIdClaim, tenantId);
            return Unauthorized($"You do not have access to organization {tenantId}.");
        }

        // Get current user's platform user info
        var platformUser = await _db.PlatformUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(pu => pu.Id == platformUserIdClaim);

        if (platformUser == null)
        {
            _logger.LogError("PlatformUser not found for ID {PlatformUserId}", platformUserIdClaim);
            return StatusCode(500, "An error occurred during tenant switch.");
        }

        // Build new claims with updated tenant context
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? ""),
            new Claim(ClaimTypes.Email, User.FindFirst(ClaimTypes.Email)?.Value ?? ""),
            new Claim("platform_user_id", platformUserIdClaim),
            new Claim("first_name", platformUser.FirstName),
            new Claim("tenant_id", tenantId),
            new Claim("tenant_role", tenantUsership.Role.ToString()),
            new Claim("is_tenant_owner", tenantUsership.IsOwner.ToString().ToLower())
        };

        // Add platform role if exists
        var platformRoleClaim = User.FindFirst("platform_role");
        if (platformRoleClaim != null)
        {
            claims.Add(platformRoleClaim);
        }

        // Re-sign the user with new claims
        var identity = new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            IdentityConstants.ApplicationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

        _logger.LogInformation("User {PlatformUserId} switched to tenant {TenantId}",
            platformUserIdClaim, tenantId);

        // Redirect to tenant home page
        return Ok(new { message = "Tenant switch successful", redirectUrl = "/" });
    }
}
