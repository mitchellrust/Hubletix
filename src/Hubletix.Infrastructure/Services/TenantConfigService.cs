using Hubletix.Core.Entities;
using Hubletix.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Hubletix.Infrastructure.Services;

/// <summary>
/// Service for managing tenant configuration.
/// Handles reading, updating, and caching of tenant settings.
/// </summary>
public interface ITenantConfigService
{
    /// <summary>
    /// Get tenant configuration, using cache if available.
    /// </summary>
    Task<Tenant?> GetTenantAsync(string tenantId);

    /// <summary>
    /// Update tenant configuration and invalidate cache.
    /// </summary>
    Task UpdateTenantConfigAsync(string tenantId, string configJson);

    /// <summary>
    /// Invalidate cache for a specific tenant (call after external updates).
    /// </summary>
    void InvalidateCache(string tenantId);
}

public class TenantConfigService : ITenantConfigService
{
    private const string CacheKeyPrefix = "tenant:";
    private readonly AppDbContext _dbContext;
    private readonly ICacheService _cacheService;
    private readonly ILogger<TenantConfigService> _logger;

    public TenantConfigService(
        AppDbContext dbContext,
        ICacheService cacheService,
        ILogger<TenantConfigService> logger
    )
    {
        _dbContext = dbContext;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Tenant?> GetTenantAsync(string tenantId)
    {
        if (string.IsNullOrEmpty(tenantId))
        {
            return null;
        }

        var cacheKey = $"{CacheKeyPrefix}{tenantId}";

        return await _cacheService.GetOrSetAsync(
            cacheKey,
            async () => await _dbContext.Tenants.FindAsync(tenantId)
        );
    }

    public async Task UpdateTenantConfigAsync(string tenantId, string configJson)
    {
        var tenant = await _dbContext.Tenants.FindAsync(tenantId);

        if (tenant == null)
        {
            throw new InvalidOperationException($"Tenant with ID {tenantId} not found.");
        }

        tenant.ConfigJson = configJson;
        await _dbContext.SaveChangesAsync();

        // Invalidate cache after update
        InvalidateCache(tenantId);

        _logger.LogInformation("Updated configuration for tenant: {TenantId}", tenantId);
    }

    public void InvalidateCache(string tenantId)
    {
        var cacheKey = $"{CacheKeyPrefix}{tenantId}";
        _cacheService.Remove(cacheKey);
        _logger.LogInformation("Cache invalidated for tenant: {TenantId}", tenantId);
    }
}
