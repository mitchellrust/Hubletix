using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hubletix.Infrastructure.Services;

/// <summary>
/// Generic caching service that can be used across the application.
/// Automatically disables caching in Development mode for easier debugging.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Get a cached value, or execute the factory function to populate it.
    /// </summary>
    Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? slidingExpiration = null, TimeSpan? absoluteExpiration = null) where T : class;
    
    /// <summary>
    /// Try to get a cached value.
    /// </summary>
    bool TryGet<T>(string key, out T? value) where T : class;
    
    /// <summary>
    /// Set a value in the cache.
    /// </summary>
    void Set<T>(string key, T value, TimeSpan? slidingExpiration = null, TimeSpan? absoluteExpiration = null) where T : class;
    
    /// <summary>
    /// Remove a specific key from the cache.
    /// </summary>
    void Remove(string key);
    
    /// <summary>
    /// Remove all keys matching a pattern.
    /// </summary>
    void RemoveByPattern(string pattern);
}

public class CacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CacheService> _logger;
    private readonly bool _isDevelopment;

    // Default cache durations
    private static readonly TimeSpan DefaultSlidingExpiration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan DefaultAbsoluteExpiration = TimeSpan.FromMinutes(10);

    public CacheService(
        IMemoryCache cache,
        ILogger<CacheService> logger,
        IHostEnvironment environment
    )
    {
        _cache = cache;
        _logger = logger;
        _isDevelopment = environment.IsDevelopment();
    }

    public async Task<T?> GetOrSetAsync<T>(
        string key, 
        Func<Task<T?>> factory, 
        TimeSpan? slidingExpiration = null, 
        TimeSpan? absoluteExpiration = null) where T : class
    {
        // In development, skip cache to always get fresh data
        if (_isDevelopment)
        {
            _logger.LogDebug("Development mode: bypassing cache for key {Key}", key);
            return await factory();
        }

        if (_cache.TryGetValue(key, out T? cachedValue))
        {
            _logger.LogDebug("Cache hit for key: {Key}", key);
            return cachedValue;
        }

        _logger.LogDebug("Cache miss for key: {Key}", key);
        
        var value = await factory();
        
        if (value != null)
        {
            Set(key, value, slidingExpiration, absoluteExpiration);
        }

        return value;
    }

    public bool TryGet<T>(string key, out T? value) where T : class
    {
        // In development, always return false to bypass cache
        if (_isDevelopment)
        {
            value = null;
            return false;
        }

        return _cache.TryGetValue(key, out value);
    }

    public void Set<T>(string key, T value, TimeSpan? slidingExpiration = null, TimeSpan? absoluteExpiration = null) where T : class
    {
        // In development, don't cache
        if (_isDevelopment)
        {
            return;
        }

        var options = new MemoryCacheEntryOptions
        {
            SlidingExpiration = slidingExpiration ?? DefaultSlidingExpiration,
            AbsoluteExpirationRelativeToNow = absoluteExpiration ?? DefaultAbsoluteExpiration
        };

        _cache.Set(key, value, options);
        _logger.LogDebug("Cached value for key: {Key}", key);
    }

    public void Remove(string key)
    {
        _cache.Remove(key);
        _logger.LogInformation("Removed cache key: {Key}", key);
    }

    public void RemoveByPattern(string pattern)
    {
        // Note: IMemoryCache doesn't natively support pattern-based removal
        // This is a simplified implementation - for production, consider using a distributed cache
        _logger.LogWarning("Pattern-based cache removal not fully implemented for in-memory cache: {Pattern}", pattern);
    }
}
