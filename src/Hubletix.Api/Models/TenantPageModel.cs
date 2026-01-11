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
/// Tenant page model that provides tenant context to all inheriting pages.
/// Automatically injects and caches the current tenant information.
/// Requires authentication and validates active tenant membership.
/// </summary>
[Authorize]
public class TenantPageModel : PageModel
{
    private IMultiTenantContextAccessor<ClubTenantInfo> _multiTenantContextAccessor { get; }
    protected AppDbContext DbContext { get; }
    protected ITenantConfigService TenantConfigService { get; }
    protected TenantConfig TenantConfig { get; set; } = new TenantConfig();
    
    /// <summary>
    /// Gets the current tenant info. Returns null if not in a tenant context.
    /// Pages requiring tenant context should check for null or use HasTenantContext.
    /// </summary>
    public ClubTenantInfo? CurrentTenantInfo => 
        _multiTenantContextAccessor.MultiTenantContext?.TenantInfo;

    /// <summary>
    /// Returns true if the page is being accessed in a tenant context.
    /// </summary>
    public bool HasTenantContext => CurrentTenantInfo != null;

    public TenantPageModel(
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
        // Verify user has active membership in current tenant
        if (HasTenantContext && CurrentTenantInfo != null)
        {
            var platformUserId = User?.FindFirst("platform_user_id")?.Value;
            
            if (!string.IsNullOrEmpty(platformUserId))
            {
                var tenantUser = await DbContext.GetTenantUserAsync(
                    platformUserId, 
                    CurrentTenantInfo.Id
                );

                // Check if user is an active member of this tenant
                if (tenantUser == null || tenantUser.Status != TenantUserStatus.Active)
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
                
                // Build navbar for public layout
                ViewData["Navbar"] = BuildNavbar();
            }
        }
        
        await next();
    }
    
    /// <summary>
    /// Builds the navbar view model based on tenant configuration and feature flags.
    /// Only call this when in a tenant context.
    /// </summary>
    protected NavbarViewModel BuildNavbar()
    {
        if (!HasTenantContext || CurrentTenantInfo == null)
        {
            return new NavbarViewModel { NavItems = new List<NavItem>() };
        }

        var navItems = new List<NavItem>();
        
        // Conditionally add nav items based on feature flags
        if (TenantConfig.Features.EnableMemberships)
        {
            navItems.Add(new() { Text = "Memberships", Url = "/membershipplans", IsActive = false });
        }
        if (TenantConfig.Features.EnableEventRegistration)
        {
            navItems.Add(new() { Text = "Events", Url = "/events", IsActive = false });
        }

        return new NavbarViewModel
        {
            TenantName = CurrentTenantInfo.Name ?? CurrentTenantInfo.Identifier,
            LogoUrl = TenantConfig.Theme.LogoUrl,
            PrimaryColor = TenantConfig.Theme.PrimaryColor,
            NavItems = navItems,
            ShowLogInButton = TenantConfig.Features.EnableUserSignup
        };
    }
}
