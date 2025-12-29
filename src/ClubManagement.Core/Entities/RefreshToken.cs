namespace ClubManagement.Core.Entities;

/// <summary>
/// Refresh token for JWT authentication with rotation and revocation support.
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }
    
    /// <summary>
    /// User ID who owns this token
    /// </summary>
    public string UserId { get; set; } = default!;
    
    /// <summary>
    /// SHA256 hash of the refresh token (never store raw token)
    /// </summary>
    public string TokenHash { get; set; } = default!;
    
    /// <summary>
    /// When the token expires
    /// </summary>
    public DateTime ExpiresAt { get; set; }
    
    /// <summary>
    /// When the token was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// IP address that requested the token
    /// </summary>
    public string CreatedByIp { get; set; } = default!;
    
    /// <summary>
    /// When the token was revoked (if revoked)
    /// </summary>
    public DateTime? RevokedAt { get; set; }
    
    /// <summary>
    /// IP address that revoked the token
    /// </summary>
    public string? RevokedByIp { get; set; }
    
    /// <summary>
    /// Hash of the new token that replaced this one (for token rotation)
    /// </summary>
    public string? ReplacedByTokenHash { get; set; }
    
    /// <summary>
    /// Reason for revocation (optional)
    /// </summary>
    public string? RevocationReason { get; set; }
    
    /// <summary>
    /// Whether this token is currently active (not revoked and not expired)
    /// </summary>
    public bool IsActive => RevokedAt == null && DateTime.UtcNow <= ExpiresAt;
    
    // Navigation property
    public User User { get; set; } = null!;
}
