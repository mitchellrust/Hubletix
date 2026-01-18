using Hubletix.Api.Models;
using Finbuckle.MultiTenant.Abstractions;
using Hubletix.Infrastructure.Persistence;

namespace Hubletix.Api.Pages.Platform;

public class ErrorModel : PlatformPageModel
{
    public ErrorModel(
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor)
        : base(multiTenantContextAccessor)
    {
    }

    public void OnGet()
    {
    }
}
