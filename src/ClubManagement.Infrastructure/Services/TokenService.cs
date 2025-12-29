using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ClubManagement.Core.Entities;
using ClubManagement.Core.Models;
using ClubManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ClubManagement.Infrastructure.Services;

public interface ITokenService
{
    Task<(string accessToken, string refreshToken)> CreateTokensAsync(User user, string? tenantId = null, CancellationToken ct = default);
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
        User user,
        string? tenantId = null,
        CancellationToken ct = default)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Get platform roles from Identity
        var platformRoles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Name, $"{user.FirstName} {user.LastName}"),
            new Claim("first_name", user.FirstName ?? string.Empty),
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
            // Load tenant role from TenantUserRole
            var tenantRole = await _db.Set<TenantUserRole>()
                .Where(tr => tr.UserId == user.Id && tr.TenantId == tenantId)
                .Select(tr => tr.Role)
                .FirstOrDefaultAsync(ct);

            claims.Add(new Claim("tenant_id", tenantId));

            if (!string.IsNullOrEmpty(tenantRole))
            {
                claims.Add(new Claim("tenant_role", tenantRole));
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
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpirationDays),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = "server" // Will be updated by controller
        };

        _db.RefreshTokens.Add(rt);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created tokens for user {UserId} (tenant: {TenantId})", user.Id, tenantId ?? "none");

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

        // Create new access token (without tenant context - can be added later)
        var tokens = await CreateTokensAsync(dbToken.User, tenantId: dbToken.User.TenantId, ct);
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
