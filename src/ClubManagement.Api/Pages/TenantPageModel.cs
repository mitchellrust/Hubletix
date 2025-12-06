using Microsoft.AspNetCore.Mvc.RazorPages;
using Finbuckle.MultiTenant.Abstractions;
using ClubManagement.Infrastructure.Persistence;

namespace ClubManagement.Api.Pages;

/// <summary>
/// Base page model that provides tenant context to all inheriting pages.
/// Automatically injects and caches the current tenant information.
/// </summary>
public class TenantPageModel : PageModel
{
    protected readonly IMultiTenantContextAccessor<ClubTenantInfo> MultiTenantContextAccessor;

    public ClubTenantInfo CurrentTenantInfo => MultiTenantContextAccessor.MultiTenantContext?.TenantInfo
      ?? throw new InvalidOperationException("Tenant information is not available in the current context.");

    public TenantPageModel(IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor)
    {
        MultiTenantContextAccessor = multiTenantContextAccessor;
    }
}
