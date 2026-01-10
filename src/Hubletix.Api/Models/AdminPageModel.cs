using Microsoft.AspNetCore.Mvc.RazorPages;
using Finbuckle.MultiTenant.Abstractions;
using Hubletix.Infrastructure.Persistence;
using Hubletix.Core.Models;
using Hubletix.Infrastructure.Services;
using Hubletix.Api.Utils;
using Hubletix.Api.Models;
using Hubletix.Api.Services;

namespace Hubletix.Api.Pages;

/// <summary>
/// Admin page model that provides tenant context to all inheriting pages.
/// Automatically injects and caches the current tenant information.
/// </summary>
public class AdminPageModel : PageModel
{
    private IMultiTenantContextAccessor<ClubTenantInfo> _multiTenantContextAccessor { get; }
    protected AppDbContext DbContext { get; }
    protected ITenantConfigService TenantConfigService { get; }
    protected IUserContextService UserContext { get; }
    protected TenantConfig TenantConfig { get; set; } = new TenantConfig();
    public ClubTenantInfo CurrentTenantInfo => _multiTenantContextAccessor.MultiTenantContext?.TenantInfo
      ?? throw new InvalidOperationException("Tenant information is not available in the current context.");

    /// <summary>
    /// Gets the current authenticated user's platform user ID.
    /// </summary>
    public string? CurrentUserId => UserContext.PlatformUserId;

    /// <summary>
    /// Gets the current user's role within the current tenant.
    /// </summary>
    public string? TenantRole => UserContext.TenantRole;

    /// <summary>
    /// Gets whether the current user is the tenant owner.
    /// </summary>
    public bool IsOwner => UserContext.IsOwner;

    /// <summary>
    /// Gets whether the current user is a tenant admin.
    /// </summary>
    public bool IsAdmin => TenantRole == "Admin";

    public AdminPageModel(
      IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor,
      ITenantConfigService tenantConfigService,
      AppDbContext dbContext,
      IUserContextService userContext
    )
    {
        _multiTenantContextAccessor = multiTenantContextAccessor;
        TenantConfigService = tenantConfigService;
        DbContext = dbContext;
        UserContext = userContext;
    }

    public override async Task OnPageHandlerExecutionAsync(
        Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutingContext context,
        Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutionDelegate next
    )
    {
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
