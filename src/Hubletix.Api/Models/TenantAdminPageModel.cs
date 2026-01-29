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
/// Tenant admin page model that provides tenant context to all inheriting pages.
/// Automatically injects and caches the current tenant information.
/// </summary>
[Authorize(Policy = "TenantAdmin")]
public class TenantAdminPageModel : PageModel
{
    private IMultiTenantContextAccessor<ClubTenantInfo> _multiTenantContextAccessor { get; }
    private ILogger<TenantAdminPageModel> _logger { get; }
    protected AppDbContext DbContext { get; }
    protected ITenantConfigService TenantConfigService { get; }
    protected TenantConfig TenantConfig { get; set; } = new TenantConfig();
    public ClubTenantInfo CurrentTenantInfo => _multiTenantContextAccessor.MultiTenantContext?.TenantInfo
      ?? throw new InvalidOperationException("Tenant information is not available in the current context.");

    /// <summary>
    /// Returns true if the page is being accessed in a tenant context, which
    /// should always be the case.
    /// </summary>
    public bool HasTenantContext => CurrentTenantInfo != null;

    public TenantAdminPageModel(
      IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor,
      ITenantConfigService tenantConfigService,
      AppDbContext dbContext,
      ILogger<TenantAdminPageModel> logger
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

        // Verify tenant user exists, is active for this platform user and has admin role
        var hasAdminRole = await DbContext.HasRoleInTenantAsync(
            platformUserId,
            CurrentTenantInfo.Id,
            TenantRole.Admin
        );
        if (!hasAdminRole)
        {
            _logger.LogWarning(
                "User {PlatformUserId} attempted to access tenant {TenantId} without active membership or admin role",
                platformUserId,
                CurrentTenantInfo.Id
            );
            context.Result = new RedirectToPageResult("/Platform/Unauthorized");
            return;
        }

        // Fetch tenant config before page handler executes
        var tenant = await TenantConfigService.GetTenantAsync(CurrentTenantInfo.Id);
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

        // Set tenant information in ViewData for use in layouts
        ViewData["TenantName"] = CurrentTenantInfo.Name;

        await next();
    }
}
