using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Finbuckle.MultiTenant.AspNetCore;
using ClubManagement.Infrastructure.Persistence;
using ClubManagement.Core.Constants;

namespace ClubManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TenantsController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IMultiTenantContext<ClubTenantInfo> _multiTenantContext;

    public TenantsController(ApplicationDbContext dbContext, IMultiTenantContext<ClubTenantInfo> multiTenantContext)
    {
        _dbContext = dbContext;
        _multiTenantContext = multiTenantContext;
    }

    [HttpGet("current")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCurrentTenant()
    {
        if (_multiTenantContext?.TenantInfo == null)
        {
            return NotFound("No tenant context set");
        }

        if (!Guid.TryParse(_multiTenantContext.TenantInfo.Id, out var tenantId))
        {
            return BadRequest("Invalid tenant ID");
        }

        var tenant = await _dbContext.Tenants.FindAsync(tenantId);
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
