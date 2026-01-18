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

public class TenantCardDto
{
    public string Name { get; set; } = string.Empty;
    public string Subdomain { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsOwner { get; set; }
}

[Authorize]
public class TenantSelectorModel : PlatformPageModel
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<TenantSelectorModel> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public List<TenantCardDto>? TenantCards { get; set; }

    [TempData]
    public string? TenantSelectorErrorMessage { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public TenantSelectorModel(
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor,
        AppDbContext dbContext,
        ILogger<TenantSelectorModel> logger,
        IHttpContextAccessor httpContextAccessor)
        : base(multiTenantContextAccessor)
    {
        _dbContext = dbContext;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
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
            var filteredTenants = tenantUsers
                .Where(
                    tu => tu.Tenant.Status == TenantStatus.Active ||
                          tu.IsOwner
                )
                .ToList();

            // Build redirect URLs server-side
            TenantCards = filteredTenants.Select(tu => BuildTenantCardDto(tu)).ToList();

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tenants for user {UserId}", PlatformUserId);
            TenantSelectorErrorMessage = "An error occurred while loading your organizations.";
            TenantCards = new List<TenantCardDto>();
            return Page();
        }
    }

    private TenantCardDto BuildTenantCardDto(TenantUser tenantUser)
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        var protocol = request?.Scheme ?? "http";
        var hostname = request?.Host.Host ?? "hubletix.home";
        var port = request?.Host.Port;
        var portString = port.HasValue && port.Value != 80 && port.Value != 443 
            ? $":{port.Value}" 
            : string.Empty;

        string baseDomain;
        if (hostname == "hubletix.home" || hostname == "127.0.0.1" || hostname == "localhost")
        {
            // Development environment
            baseDomain = hostname;
        }
        else
        {
            // Production environment - extract base domain
            var parts = hostname.Split('.');
            baseDomain = parts.Length > 2 
                ? string.Join(".", parts.Skip(parts.Length - 2)) 
                : hostname;
        }

        var returnPath = !string.IsNullOrEmpty(ReturnUrl) ? ReturnUrl : "/";
        var redirectUrl = $"{protocol}://{tenantUser.Tenant.Subdomain}.{baseDomain}{portString}{returnPath}";

        return new TenantCardDto
        {
            Name = tenantUser.Tenant.Name,
            Subdomain = tenantUser.Tenant.Subdomain,
            RedirectUrl = redirectUrl,
            Role = tenantUser.Role.ToString(),
            IsOwner = tenantUser.IsOwner
        };
    }
}
