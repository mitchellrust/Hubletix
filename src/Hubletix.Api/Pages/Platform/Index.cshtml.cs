using Finbuckle.MultiTenant.Abstractions;
using Hubletix.Api.Models;
using Hubletix.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace Hubletix.Api.Pages.Platform;

public class IndexModel : PlatformPageModel
{
    public IndexModel(
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor)
        : base(multiTenantContextAccessor)
    {
    }

    public IActionResult OnGet()
    {
        // Show public landing page
        return Page();
    }
}
