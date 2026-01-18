using Hubletix.Infrastructure.Persistence;
using Hubletix.Infrastructure.Services;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Authorization;

namespace Hubletix.Api.Pages.Tenant;

[AllowAnonymous]
public class AccessDeniedModel : TenantPageModel
{
    public AccessDeniedModel(
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor,
        ILogger<AccessDeniedModel> logger,
        ITenantConfigService tenantConfigService,
        AppDbContext dbContext)
        : base(multiTenantContextAccessor, logger, tenantConfigService, dbContext)
    {
    }

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public void OnGet()
    {
    }
}
