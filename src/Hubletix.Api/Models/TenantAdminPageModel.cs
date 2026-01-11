using Microsoft.AspNetCore.Mvc.RazorPages;
using Finbuckle.MultiTenant.Abstractions;
using Hubletix.Infrastructure.Persistence;
using Hubletix.Core.Models;
using Hubletix.Infrastructure.Services;
using Hubletix.Api.Utils;
using Hubletix.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Hubletix.Core.Enums;

namespace Hubletix.Api.Pages;

/// <summary>
/// Tenant admin page model that provides tenant context to all inheriting pages.
/// Automatically injects and caches the current tenant information.
/// </summary>
[Authorize(Policy = "TenantAdmin")]
public class TenantAdminPageModel : PageModel
{
    private IMultiTenantContextAccessor<ClubTenantInfo> _multiTenantContextAccessor { get; }
    protected AppDbContext DbContext { get; }
    protected ITenantConfigService TenantConfigService { get; }
    protected TenantConfig TenantConfig { get; set; } = new TenantConfig();
    public ClubTenantInfo CurrentTenantInfo => _multiTenantContextAccessor.MultiTenantContext?.TenantInfo
      ?? throw new InvalidOperationException("Tenant information is not available in the current context.");

    public TenantAdminPageModel(
      IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor,
      ITenantConfigService tenantConfigService,
      AppDbContext dbContext
    )
    {
        _multiTenantContextAccessor = multiTenantContextAccessor;
        TenantConfigService = tenantConfigService;
        DbContext = dbContext;
    }

    public override async Task OnPageHandlerExecutionAsync(
        Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutingContext context,
        Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutionDelegate next
    )
    {
        // Verify user has Admin role in current tenant with Active status
        var platformUserId = User?.FindFirst("platform_user_id")?.Value;
        
        if (!string.IsNullOrEmpty(platformUserId))
        {
            var hasAdminRole = await DbContext.HasRoleInTenantAsync(
                platformUserId,
                CurrentTenantInfo.Id,
                TenantRole.Admin
            );

            if (!hasAdminRole)
            {
                context.Result = new RedirectToPageResult("/Tenant/AccessDenied");
                return;
            }
        }
        
        // Set tenant information in ViewData for use in layouts
        ViewData["TenantName"] = CurrentTenantInfo.Name;

        // Fetch tenant config before page handler executes
        var tenant = await TenantConfigService.GetTenantAsync(CurrentTenantInfo.Id);
        if (tenant != null)
        {
            TenantConfig = tenant.GetConfig();
            // Set view data for layout usage
            ViewData["TenantConfig"] = TenantConfig;
            
            // Set logo URL if available in tenant config
            if (!string.IsNullOrEmpty(TenantConfig.Theme.LogoUrl))
            {
                ViewData["TenantLogoUrl"] = TenantConfig.Theme.LogoUrl;
            }
        }
        
        await next();
    }
    
    /// <summary>
    /// Builds the navbar view model based on tenant configuration and feature flags.
    /// </summary>
    protected NavbarViewModel BuildNavbar()
    {
        var navItems = new List<NavItem>();
        
        // Conditionally add nav items based on feature flags
        if (TenantConfig?.Features?.EnableMemberships ?? false)
        {
            navItems.Add(new() { Text = "Memberships", Url = "/membershipplans", IsActive = false });
        }
        if (TenantConfig?.Features?.EnableEventRegistration ?? false)
        {
            navItems.Add(new() { Text = "Events", Url = "/events", IsActive = false });
        }
        navItems.Add(new() { Text = "Contact Us", Url = "/contact", IsActive = false });

        return new NavbarViewModel
        {
            TenantName = CurrentTenantInfo.Name ?? CurrentTenantInfo.Identifier,
            LogoUrl = TenantConfig?.Theme?.LogoUrl,
            PrimaryColor = TenantConfig?.Theme?.PrimaryColor,
            NavItems = navItems,
            ShowLogInButton = TenantConfig?.Features?.EnableUserSignup ?? false
        };
    }
}
