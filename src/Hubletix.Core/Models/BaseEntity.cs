namespace Hubletix.Core.Models;

/// <summary>
/// Base entity for all multi-tenant entities in the application.
/// Provides TenantId for multi-tenancy and common audit properties.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// Unique identifier for the entity.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Tenant ID for multi-tenancy isolation.
    /// All queries should be filtered by this property.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// When the entity was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User ID or system identifier that created the entity.
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// When the entity was last modified.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// User ID or system identifier that last modified the entity.
    /// </summary>
    public string? UpdatedBy { get; set; }
}
