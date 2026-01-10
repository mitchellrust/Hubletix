using Finbuckle.MultiTenant.Abstractions;
using Hubletix.Api.Models;
using Hubletix.Core.Entities;
using Hubletix.Core.Enums;
using Hubletix.Infrastructure.Persistence;
using Hubletix.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hubletix.Api.Pages.Platform;

[Authorize]
public class TenantSelectorModel : PlatformPageModel
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<TenantSelectorModel> _logger;

    public List<TenantUser>? UserTenants { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public TenantSelectorModel(
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor,
        AppDbContext dbContext,
        ILogger<TenantSelectorModel> logger)
        : base(multiTenantContextAccessor)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!IsAuthenticated || string.IsNullOrEmpty(PlatformUserId))
        {
            _logger.LogWarning("Unauthenticated user attempted to access TenantSelector");
            return RedirectToPage("/Platform/Login");
        }

        try
        {
            // Fetch user's tenants using extension method
            UserTenants = await _dbContext.GetUserTenantsAsync(
                PlatformUserId,
                TenantUserStatus.Active
            );

            _logger.LogInformation("User {UserId} has {TenantCount} active tenants",
                PlatformUserId, UserTenants.Count);

            // Auto-redirect if user has exactly one tenant
            if (UserTenants.Count == 1)
            {
                var tenant = UserTenants.First().Tenant;
                _logger.LogInformation("Auto-redirecting user {UserId} to their only tenant: {TenantId}",
                    PlatformUserId, tenant.Id);

                // Build tenant URL
                var tenantUrl = BuildTenantUrl(tenant.Subdomain);
                return Redirect(tenantUrl);
            }

            // Show selector if user has 0 or 2+ tenants
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tenants for user {UserId}", PlatformUserId);
            ErrorMessage = "An error occurred while loading your organizations.";
            UserTenants = new List<TenantUser>();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        // Sign out using cookie authentication
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        _logger.LogInformation("User {UserId} logged out from platform", PlatformUserId);

        return RedirectToPage("/Platform/Login");
    }

    /// <summary>
    /// Builds the full tenant URL based on the current environment
    /// </summary>
    private string BuildTenantUrl(string tenantIdentifier)
    {
        var request = HttpContext.Request;
        var scheme = request.Scheme;
        var host = request.Host;

        // Development environment (localhost)
        if (host.Host == "localhost" || host.Host == "127.0.0.1")
        {
            var port = host.Port.HasValue ? $":{host.Port}" : "";
            return $"{scheme}://{tenantIdentifier}.{host.Host}{port}/";
        }

        // Production environment
        // Extract base domain (remove subdomain if present)
        var hostParts = host.Host.Split('.');
        string baseDomain;

        if (hostParts.Length > 2)
        {
            // Has subdomain, take last two parts (e.g., hubletix.app)
            baseDomain = string.Join(".", hostParts.Skip(hostParts.Length - 2));
        }
        else
        {
            // No subdomain, use as-is
            baseDomain = host.Host;
        }

        var portStr = host.Port.HasValue ? $":{host.Port}" : "";
        return $"{scheme}://{tenantIdentifier}.{baseDomain}{portStr}/";
    }
}
