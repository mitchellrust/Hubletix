using Microsoft.AspNetCore.Mvc.RazorPages;
using Finbuckle.MultiTenant.Abstractions;
using ClubManagement.Infrastructure.Persistence;
using ClubManagement.Core.Models;
using ClubManagement.Infrastructure.Services;
using ClubManagement.Api.Utils;

namespace ClubManagement.Api.Pages;

/// <summary>
/// Base page model that provides tenant context to all inheriting pages.
/// Automatically injects and caches the current tenant information.
/// </summary>
public class TenantPageModel : PageModel
{
    private readonly IMultiTenantContextAccessor<ClubTenantInfo> _multiTenantContextAccessor;
    public readonly AppDbContext AppDbContext;
    private readonly ITenantConfigService _tenantConfigService;
    public required TenantConfig TenantConfig { get; set; }
    public ClubTenantInfo CurrentTenantInfo => _multiTenantContextAccessor.MultiTenantContext?.TenantInfo
      ?? throw new InvalidOperationException("Tenant information is not available in the current context.");

    public TenantPageModel(
      IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor,
      ITenantConfigService tenantConfigService,
      AppDbContext appDbContext
    )
    {
        _multiTenantContextAccessor = multiTenantContextAccessor;
        _tenantConfigService = tenantConfigService;
        AppDbContext = appDbContext;
    }

    public override async Task OnPageHandlerExecutionAsync(
        Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutingContext context,
        Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutionDelegate next
    )
    {
        // Set tenant information in ViewData for use in layouts
        ViewData["TenantName"] = CurrentTenantInfo.Name;

        // Fetch tenant config before page handler executes
        var tenant = await _tenantConfigService.GetTenantAsync(CurrentTenantInfo.Id);
        if (tenant != null)
        {
            TenantConfig = tenant.GetConfig();
            
            // Set logo URL if available in tenant config
            if (!string.IsNullOrEmpty(TenantConfig.Theme.LogoUrl))
            {
                ViewData["TenantLogoUrl"] = TenantConfig.Theme.LogoUrl;
            }
        }
        
        await next();
    }
}
