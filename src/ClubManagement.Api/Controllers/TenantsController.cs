using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Finbuckle.MultiTenant.Abstractions;
using ClubManagement.Infrastructure.Persistence;
using ClubManagement.Infrastructure.Services;

namespace ClubManagement.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class TenantsController : ControllerBase
{
    private readonly IMultiTenantContextAccessor<ClubTenantInfo> _multiTenantContextAccessor;
    private readonly ITenantConfigCacheService _tenantConfigCache;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(
        AppDbContext dbContext,
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor,
        ITenantConfigCacheService tenantConfigCache,
        ILogger<TenantsController> logger
    )
    {
        _multiTenantContextAccessor = multiTenantContextAccessor;
        _tenantConfigCache = tenantConfigCache;
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

        var tenant = await _tenantConfigCache.GetTenantConfigAsync(
            _multiTenantContextAccessor.MultiTenantContext.TenantInfo.Id);

        if (tenant == null)
        {
            return NotFound("Tenant configuration not found");
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
