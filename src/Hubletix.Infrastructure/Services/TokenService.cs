using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Hubletix.Core.Entities;
using Hubletix.Core.Models;
using Hubletix.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Hubletix.Infrastructure.Services;

public interface ITokenService
{
    Task<(string accessToken, string refreshToken)> CreateTokensAsync(User identityUser, string platformUserId, string? tenantId = null, CancellationToken ct = default);
    Task<(string accessToken, string refreshToken)> RefreshAsync(string refreshToken, string ipAddress, CancellationToken ct = default);
    Task RevokeRefreshTokenAsync(string refreshToken, string ipAddress, string? reason = null, CancellationToken ct = default);
}

public class TokenService : ITokenService
{
    private readonly UserManager<User> _userManager;
    private readonly JwtSettings _jwt;
    private readonly AppDbContext _db;
    private readonly ILogger<TokenService> _logger;

    public TokenService(
        UserManager<User> userManager,
        IOptions<JwtSettings> jwtOptions,
        AppDbContext db,
        ILogger<TokenService> logger)
    {
        _userManager = userManager;
        _jwt = jwtOptions.Value;
        _db = db;
        _logger = logger;
    }

    public async Task<(string accessToken, string refreshToken)> CreateTokensAsync(
        User identityUser,
        string platformUserId,
        string? tenantId = null,
        CancellationToken ct = default)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

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
            new Claim(JwtRegisteredClaimNames.Sub, identityUser.Id),
            new Claim(JwtRegisteredClaimNames.Email, identityUser.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Name, platformUser.FullName),
            new Claim("first_name", platformUser.FirstName),
            new Claim("platform_user_id", platformUserId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
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

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpirationMinutes),
            signingCredentials: creds
        );

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        // Create refresh token
        var refreshToken = GenerateRandomToken();
        var refreshTokenHash = ComputeSha256Hash(refreshToken);

        var rt = new RefreshToken
        {
            UserId = identityUser.Id, // Store IdentityUser.Id for auth-level token
            TokenHash = refreshTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpirationDays),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = "server" // Will be updated by controller
        };

        _db.RefreshTokens.Add(rt);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created tokens for user {PlatformUserId} (tenant: {TenantId})", platformUser.Id, tenantId ?? "none");

        return (accessToken, refreshToken);
    }

    public async Task<(string accessToken, string refreshToken)> RefreshAsync(
        string refreshToken,
        string ipAddress,
        CancellationToken ct = default)
    {
        var hash = ComputeSha256Hash(refreshToken);
        var dbToken = await _db.RefreshTokens
            .Include(r => r.User)
            .Where(r => r.TokenHash == hash)
            .FirstOrDefaultAsync(ct);

        if (dbToken == null || !dbToken.IsActive)
        {
            _logger.LogWarning("Invalid or inactive refresh token from IP {IpAddress}", ipAddress);
            throw new SecurityTokenException("Invalid refresh token");
        }

        // Rotate token
        dbToken.RevokedAt = DateTime.UtcNow;
        dbToken.RevokedByIp = ipAddress;

        var newRefreshToken = GenerateRandomToken();
        var newHash = ComputeSha256Hash(newRefreshToken);
        dbToken.ReplacedByTokenHash = newHash;

        var newDbToken = new RefreshToken
        {
            UserId = dbToken.UserId,
            TokenHash = newHash,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpirationDays),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = ipAddress
        };

        _db.RefreshTokens.Add(newDbToken);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Refresh token rotated for user {UserId} from IP {IpAddress}", dbToken.UserId, ipAddress);

        // Fetch PlatformUser to get DefaultTenantId and platformUserId
        var platformUser = await _db.PlatformUsers
            .FirstOrDefaultAsync(pu => pu.IdentityUserId == dbToken.UserId, ct);
        
        if (platformUser == null)
        {
            throw new InvalidOperationException($"PlatformUser not found for IdentityUser {dbToken.UserId}");
        }

        // Create new access token with default tenant context
        var tokens = await CreateTokensAsync(dbToken.User, platformUser.Id, platformUser.DefaultTenantId, ct);
        return (tokens.accessToken, newRefreshToken);
    }

    public async Task RevokeRefreshTokenAsync(
        string refreshToken,
        string ipAddress,
        string? reason = null,
        CancellationToken ct = default)
    {
        var hash = ComputeSha256Hash(refreshToken);
        var dbToken = await _db.RefreshTokens
            .Where(r => r.TokenHash == hash)
            .FirstOrDefaultAsync(ct);

        if (dbToken == null || dbToken.RevokedAt != null)
        {
            return; // Already revoked or doesn't exist
        }

        dbToken.RevokedAt = DateTime.UtcNow;
        dbToken.RevokedByIp = ipAddress;
        dbToken.RevocationReason = reason;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Refresh token revoked for user {UserId} from IP {IpAddress}. Reason: {Reason}",
            dbToken.UserId, ipAddress, reason ?? "none");
    }

    private static string GenerateRandomToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    private static string ComputeSha256Hash(string raw)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }
}
