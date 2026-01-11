using System.Security.Claims;
using Hubletix.Core.Entities;
using Hubletix.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hubletix.Infrastructure.Services;

public interface IClaimsPrincipalFactory
{
    Task<ClaimsPrincipal> CreateClaimsPrincipalAsync(User identityUser, string platformUserId, string? tenantId = null, CancellationToken ct = default);
}

public class ClaimsPrincipalFactory : IClaimsPrincipalFactory
{
    private readonly UserManager<User> _userManager;
    private readonly AppDbContext _db;
    private readonly ILogger<ClaimsPrincipalFactory> _logger;

    public ClaimsPrincipalFactory(
        UserManager<User> userManager,
        AppDbContext db,
        ILogger<ClaimsPrincipalFactory> logger)
    {
        _userManager = userManager;
        _db = db;
        _logger = logger;
    }

    public async Task<ClaimsPrincipal> CreateClaimsPrincipalAsync(
        User identityUser,
        string platformUserId,
        string? tenantId = null,
        CancellationToken ct = default)
    {
        // Get platform roles from Identity
        var platformRoles = await _userManager.GetRolesAsync(identityUser);

        // Get PlatformUser for name info
        var platformUser = await _db.PlatformUsers
            .FirstOrDefaultAsync(pu => pu.Id == platformUserId, ct);

        if (platformUser == null)
        {
            throw new InvalidOperationException($"PlatformUser {platformUserId} not found");
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, identityUser.Id),
            new Claim(ClaimTypes.Email, identityUser.Email ?? string.Empty),
            new Claim(ClaimTypes.Name, platformUser.FullName),
            new Claim("first_name", platformUser.FirstName),
            new Claim("platform_user_id", platformUserId)
        };

        // Add platform roles
        if (platformRoles.Any())
        {
            claims.Add(new Claim("platform_role", string.Join(",", platformRoles)));
        }

        // Add tenant-specific information
        if (!string.IsNullOrEmpty(tenantId))
        {
            // Load tenant role from TenantUser
            var tenantUser = await _db.TenantUsers
                .Where(tu => tu.PlatformUserId == platformUserId && tu.TenantId == tenantId)
                .FirstOrDefaultAsync(ct);

            claims.Add(new Claim("tenant_id", tenantId));

            if (tenantUser != null)
            {
                claims.Add(new Claim("tenant_role", tenantUser.Role.ToString()));
                
                if (tenantUser.IsOwner)
                {
                    claims.Add(new Claim("is_tenant_owner", "true"));
                }
            }
        }

        var identity = new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme);
        var principal = new ClaimsPrincipal(identity);

        _logger.LogInformation("Created claims principal for user {PlatformUserId} (tenant: {TenantId})", platformUser.Id, tenantId ?? "none");

        return principal;
    }
}
