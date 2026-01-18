using Finbuckle.MultiTenant.Abstractions;
using Hubletix.Api.Models;
using Hubletix.Core.Entities;
using Hubletix.Core.Enums;
using Hubletix.Infrastructure.Persistence;
using Hubletix.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Hubletix.Core.Constants;

namespace Hubletix.Api.Pages.Platform;

[Authorize]
public class TenantSelectorModel : PlatformPageModel
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<TenantSelectorModel> _logger;

    public List<TenantUser>? UserTenants { get; set; }

    [TempData]
    public string? TenantSelectorErrorMessage { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

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
            var tenantUsers = await _dbContext.GetUserTenantsAsync(
                PlatformUserId,
                TenantUserStatus.Active
            );

            // Should only display tenants that are active.
            // Also, if a tenant is not active, only display if user is owner
            // so they can resolve the outstanding action.
            UserTenants = tenantUsers
                .Where(
                    tu => tu.Tenant.Status == TenantStatus.Active ||
                          tu.IsOwner
                )
                .ToList();

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tenants for user {UserId}", PlatformUserId);
            TenantSelectorErrorMessage = "An error occurred while loading your organizations.";
            UserTenants = new List<TenantUser>();
            return Page();
        }
    }
}
