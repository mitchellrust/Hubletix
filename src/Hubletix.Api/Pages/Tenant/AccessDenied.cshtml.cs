using Hubletix.Infrastructure.Persistence;
using Hubletix.Infrastructure.Services;
using Finbuckle.MultiTenant.Abstractions;

namespace Hubletix.Api.Pages.Tenant;

public class AccessDeniedModel : TenantPageModel
{
    public AccessDeniedModel(
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor,
        ITenantConfigService tenantConfigService,
        AppDbContext dbContext)
        : base(multiTenantContextAccessor, tenantConfigService, dbContext)
    {
    }

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public void OnGet()
    {
    }
}
