using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Finbuckle.MultiTenant.Abstractions;
using ClubManagement.Infrastructure.Persistence;

namespace ClubManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TenantsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IMultiTenantContextAccessor<ClubTenantInfo> _multiTenantContextAccessor;

    public TenantsController(AppDbContext dbContext, IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor)
    {
        _dbContext = dbContext;
        _multiTenantContextAccessor = multiTenantContextAccessor;
    }

    [HttpGet("current")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCurrentTenant()
    {
        if (_multiTenantContextAccessor.MultiTenantContext.TenantInfo == null)
        {
            return NotFound("No tenant context set");
        }

        var tenant = await _dbContext.Tenants.FindAsync(_multiTenantContextAccessor.MultiTenantContext.TenantInfo.Id);
        if (tenant == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            tenant.Id,
            tenant.Name,
            tenant.Subdomain,
            tenant.IsActive,
            tenant.CreatedAt,
            tenant.UpdatedAt
        });
    }

    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
}
