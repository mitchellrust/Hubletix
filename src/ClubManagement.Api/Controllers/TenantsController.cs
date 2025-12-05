using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Finbuckle.MultiTenant.Abstractions;
using ClubManagement.Infrastructure.Persistence;
using Microsoft.Extensions.Caching.Memory;
using ClubManagement.Core.Entities;

namespace ClubManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TenantsController : ControllerBase
{
    private const string TenantConfigCachePrefix = "tenantconfig:";
    private readonly AppDbContext _dbContext;
    private readonly IMultiTenantContextAccessor<ClubTenantInfo> _multiTenantContextAccessor;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(
        AppDbContext dbContext,
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor,
        IMemoryCache cache,
        ILogger<TenantsController> logger
    )
    {
        _dbContext = dbContext;
        _multiTenantContextAccessor = multiTenantContextAccessor;
        _cache = cache;
        _logger = logger;
    }

    [HttpGet("current")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCurrentTenant()
    {
        if (_multiTenantContextAccessor.MultiTenantContext.TenantInfo == null)
        {
            return NotFound("No tenant context set");
        }

        Tenant? tenant;

        var cacheKey = $"{TenantConfigCachePrefix}{_multiTenantContextAccessor.MultiTenantContext.TenantInfo.Id}";
        if (!_cache.TryGetValue(cacheKey, out tenant))
        {
            _logger.LogInformation("Cache miss: {TenantId}", _multiTenantContextAccessor.MultiTenantContext.TenantInfo.Id);

            tenant = await _dbContext.Tenants
                .FindAsync(_multiTenantContextAccessor.MultiTenantContext.TenantInfo.Id);

            if (tenant == null)
            {
                return NotFound();
            }

            _cache.Set(
                cacheKey,
                tenant,
                new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromMinutes(5),
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                });
        }
    else
    {
      _logger.LogInformation("Cache Hit: {TenantId}", tenant!.Id);
    }

        return Ok(new
        {
            tenant!.Id,
            tenant.Name,
            tenant.Subdomain,
            tenant.IsActive,
            tenant.CreatedAt,
            tenant.UpdatedAt,
            tenant.ConfigJson
        });
    }

    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
}
