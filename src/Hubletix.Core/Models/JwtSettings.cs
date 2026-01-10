namespace ClubManagement.Core.Models;

/// <summary>
/// Configuration settings for JWT token generation and validation.
/// </summary>
public class JwtSettings
{
    /// <summary>
    /// Secret key for signing tokens (should be stored securely)
    /// </summary>
    public string Secret { get; set; } = default!;
    
    /// <summary>
    /// Token issuer (typically your app's domain)
    /// </summary>
    public string Issuer { get; set; } = default!;
    
    /// <summary>
    /// Token audience (typically your app's domain)
    /// </summary>
    public string Audience { get; set; } = default!;
    
    /// <summary>
    /// Access token expiration in minutes (default: 15 minutes)
    /// </summary>
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    
    /// <summary>
    /// Refresh token expiration in days (default: 30 days)
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 30;
}
