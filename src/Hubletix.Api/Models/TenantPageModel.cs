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
using Hubletix.Core.Constants;

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
    private ILogger<TenantPageModel> _logger { get; }
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
      ILogger<TenantPageModel> logger,
      ITenantConfigService tenantConfigService,
      AppDbContext dbContext
    )
    {
        _multiTenantContextAccessor = multiTenantContextAccessor;
        TenantConfigService = tenantConfigService;
        DbContext = dbContext;
        _logger = logger;
    }

    public override async Task OnPageHandlerExecutionAsync(
        Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutingContext context,
        Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutionDelegate next
    )
    {
        if (!HasTenantContext || CurrentTenantInfo == null)
        {
            // Not in a tenant context, requested Tenant does not exist (i.e. invalid subdomain)
            _logger.LogDebug(
                "Access attempt without tenant context. Redirecting to tenant selector."
            );
            context.Result = new RedirectToPageResult("/Platform/TenantSelector");
            return;
        }

        var platformUserId = User?.FindFirst("platform_user_id")?.Value;
        if (string.IsNullOrEmpty(platformUserId))
        {
            _logger.LogInformation(
                "Unauthenticated access attempt to tenant {TenantId}.",
                CurrentTenantInfo.Id
            );
            // Redirect to login page
            context.Result = new RedirectToPageResult("/Platform/Login");
            return;
        }

        var tenantUser = await DbContext.GetTenantUserAsync(
            platformUserId,
            CurrentTenantInfo.Id
        );
        if (tenantUser == null)
        {
            _logger.LogWarning(
                "User {PlatformUserId} attempted to access tenant {TenantId} without membership.",
                platformUserId,
                CurrentTenantInfo.Id
            );
            context.Result = new RedirectToPageResult("/Platform/TenantSelector");
            return;
        }

        // Check if user is an active member of this tenant
        if (tenantUser.Status != TenantUserStatus.Active)
        {
            _logger.LogWarning(
                "User {PlatformUserId} attempted to access tenant {TenantId} with status [{TenantUserStatus}]",
                platformUserId,
                CurrentTenantInfo.Id,
                tenantUser.Status
            );
            context.Result = new RedirectToPageResult("/Platform/Unauthorized");
            return;
        }

        // Set tenant information in ViewData for use in layouts
        ViewData["TenantName"] = CurrentTenantInfo.Name;

        // Fetch tenant config before page handler executes
        var tenant = await TenantConfigService.GetTenantAsync(CurrentTenantInfo.Id);

        // Verify we have an active tenant
        if (tenant == null)
        {
            _logger.LogError(
                "Could not find tenant [{TenantId}] but tenant context exists",
                CurrentTenantInfo.Id
            );
            // Just show an error for the end user.
            context.Result = new RedirectToPageResult("/Platform/Error");
            return;
        }
        else if (tenant.Status != TenantStatus.Active)
        {
            _logger.LogWarning(
                "Access attempt to tenant [{TenantId}] TenantStatus [{TenantStatus}] by user [{PlatformUserId}]",
                CurrentTenantInfo.Id,
                tenant?.Status,
                platformUserId
            );
            // Tenant isn't active, redirect user to tenant selector to pick an active one
            context.Result = new RedirectToPageResult("/Platform/TenantSelector");
            return;
        }

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
            ShowLogInButton = TenantConfig.Features.EnableUserSignup,
            UserEmail = User?.Identity?.Name,
            IsUserAuthenticated = User?.Identity?.IsAuthenticated ?? false,
            IsUserTenantAdmin = User?.HasClaim("tenant_role", "Admin") ?? false
        };
    }
}
