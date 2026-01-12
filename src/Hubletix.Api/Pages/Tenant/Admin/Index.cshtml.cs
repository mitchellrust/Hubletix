using Microsoft.AspNetCore.Mvc;
using Hubletix.Infrastructure.Persistence;
using Finbuckle.MultiTenant.Abstractions;
using Hubletix.Infrastructure.Services;

namespace Hubletix.Api.Pages.Tenant.Admin;

public class IndexModel : TenantAdminPageModel
{    
    public IndexModel(
        AppDbContext dbContext,
        ITenantConfigService tenantConfigService,
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor
    ) : base(
        multiTenantContextAccessor,
        tenantConfigService,
        dbContext
    )
    {
    }

    // Redirect to dashboard
    public IActionResult OnGet()
    {
        return RedirectToPage("/Tenant/Admin/Dashboard");
    }

}
