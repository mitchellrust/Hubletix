using Microsoft.Extensions.Caching.Memory;
using ClubManagement.Core.Entities;
using ClubManagement.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace ClubManagement.Infrastructure.Services;

/// <summary>
/// Service for caching and retrieving tenant configuration.
/// Abstracts cache management to avoid repetition across the application.
/// </summary>
public interface ITenantConfigCacheService
{
    /// <summary>
    /// Get tenant configuration, using cache if available.
    /// </summary>
    Task<Tenant?> GetTenantConfigAsync(string tenantId);
    
    /// <summary>
    /// Invalidate cache for a specific tenant (call after updates).
    /// </summary>
    void InvalidateCache(string tenantId);
}

public class TenantConfigCacheService : ITenantConfigCacheService
{
    private const string CacheKeyPrefix = "tenantconfig:";
    private readonly AppDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TenantConfigCacheService> _logger;

    public TenantConfigCacheService(
        AppDbContext dbContext,
        IMemoryCache cache,
        ILogger<TenantConfigCacheService> logger
    )
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Tenant?> GetTenantConfigAsync(string tenantId)
    {
        var cacheKey = $"{CacheKeyPrefix}{tenantId}";

        if (_cache.TryGetValue(cacheKey, out Tenant? cachedTenant))
        {
            _logger.LogDebug("Cache hit for tenant: {TenantId}", tenantId);
            return cachedTenant;
        }

        _logger.LogDebug("Cache miss for tenant: {TenantId}", tenantId);
        
        var tenant = await _dbContext.Tenants.FindAsync(tenantId);
        
        if (tenant != null)
        {
            _cache.Set(
                cacheKey,
                tenant,
                new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromMinutes(5),
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                });
        }

        return tenant;
    }

    public void InvalidateCache(string tenantId)
    {
        var cacheKey = $"{CacheKeyPrefix}{tenantId}";
        _cache.Remove(cacheKey);
        _logger.LogInformation("Cache invalidated for tenant: {TenantId}", tenantId);
    }
}