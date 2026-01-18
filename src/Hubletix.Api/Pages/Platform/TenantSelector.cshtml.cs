using Finbuckle.MultiTenant.Abstractions;
using Hubletix.Api.Models;
using Hubletix.Core.Entities;
using Hubletix.Core.Enums;
using Hubletix.Infrastructure.Persistence;
using Hubletix.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Hubletix.Core.Constants;
using Microsoft.EntityFrameworkCore;

namespace Hubletix.Api.Pages.Platform;

public class TenantCardDto
{
    public string Name { get; set; } = string.Empty;
    public string Subdomain { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsOwner { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusMessage { get; set; } = string.Empty;
    public string ActionText { get; set; } = string.Empty;
    public bool IsDisabled { get; set; }
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
                PlatformUserId
            );

            // Display active tenants and non-active tenants for all admins
            // Non-owner admins will see non-active tenants as disabled
            var filteredTenants = tenantUsers
                .Where(tu => tu.Tenant.Status == TenantStatus.Active || 
                           tu.Tenant.Status == TenantStatus.PendingActivation ||
                           tu.Tenant.Status == TenantStatus.Suspended)
                .ToList();

            // Build redirect URLs server-side (parallel async execution)
            var cardTasks = filteredTenants.Select(tu => BuildTenantCardDtoAsync(tu));
            TenantCards = (await Task.WhenAll(cardTasks)).ToList();

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

    private async Task<TenantCardDto> BuildTenantCardDtoAsync(TenantUser tenantUser)
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

        string redirectUrl;
        string statusMessage;
        string actionText;
        bool isDisabled = false;

        // Build different URLs and messages based on tenant status
        switch (tenantUser.Tenant.Status)
        {
            case TenantStatus.PendingActivation:
                // Find the signup session for this tenant to resume
                var signupSession = await _dbContext.SignupSessions
                    .FirstOrDefaultAsync(s => s.TenantId == tenantUser.Tenant.Id && 
                                           s.State != SignupSessionState.Completed);
                
                if (signupSession != null)
                {
                    redirectUrl = $"{protocol}://{hostname}{portString}/signup/setuporganization?sessionId={signupSession.Id}";
                }
                else
                {
                    // Fallback if no session found
                    redirectUrl = $"{protocol}://{hostname}{portString}/signup/selectplan";
                }
                statusMessage = "Setup Incomplete";
                actionText = "Continue Setup";
                isDisabled = !tenantUser.IsOwner;
                break;

            case TenantStatus.Suspended:
                // Redirect to admin dashboard so owner can address the issue
                redirectUrl = $"{protocol}://{tenantUser.Tenant.Subdomain}.{baseDomain}{portString}/admin";
                statusMessage = "Organization Suspended";
                actionText = "Resolve Issue";
                isDisabled = !tenantUser.IsOwner;
                break;

            case TenantStatus.Active:
            default:
                // Normal redirect to tenant with optional return URL
                var returnPath = !string.IsNullOrEmpty(ReturnUrl) ? ReturnUrl : "/";
                redirectUrl = $"{protocol}://{tenantUser.Tenant.Subdomain}.{baseDomain}{portString}{returnPath}";
                statusMessage = string.Empty;
                actionText = "Access Dashboard";
                break;
        }

        return new TenantCardDto
        {
            Name = tenantUser.Tenant.Name,
            Subdomain = tenantUser.Tenant.Subdomain,
            RedirectUrl = redirectUrl,
            Role = tenantUser.Role.ToString(),
            IsOwner = tenantUser.IsOwner,
            Status = tenantUser.Tenant.Status,
            StatusMessage = statusMessage,
            ActionText = actionText,
            IsDisabled = isDisabled
        };
    }
}
