using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Hubletix.Infrastructure.Persistence;
using Finbuckle.MultiTenant.Abstractions;
using Hubletix.Api.Utils;
using Hubletix.Infrastructure.Services;
using Hubletix.Core.Constants;
using System.Security.Claims;

namespace Hubletix.Api.Pages.Tenant.Admin;

public class DashboardModel : TenantAdminPageModel
{
    private readonly ITenantOnboardingService _tenantOnboardingService;
    private readonly ILogger<DashboardModel> _logger;
    private readonly ITenantConfigService _tenantConfigService;
    public List<UpcomingEventDto> UpcomingEvents { get; set; } = new();
    public TenantStatsDto TenantStats { get; set; } = new();
    public Core.Entities.Tenant? CurrentTenant { get; set; }

    [TempData]
    public string? TenantAdminDashboardErrorMessage { get; set; }

    public DashboardModel(
        AppDbContext dbContext,
        ITenantConfigService tenantConfigService,
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor,
        ITenantOnboardingService tenantOnboardingService,
        ILogger<DashboardModel> logger
    ) : base(
        multiTenantContextAccessor,
        tenantConfigService,
        dbContext,
        logger
    )
    {
        _tenantOnboardingService = tenantOnboardingService;
        _tenantConfigService = tenantConfigService;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var utcNow = DateTime.UtcNow;

        // Load current tenant for Stripe onboarding state
        CurrentTenant = await _tenantConfigService.GetTenantAsync(CurrentTenantInfo.Id);
        if (CurrentTenant == null)
        {
            _logger.LogError(
                "Current tenant not found for tenant ID {TenantId}",
                CurrentTenantInfo.Id
            );
            return RedirectToPage("/Platform/Error");
        }

        // Check if we need to refresh onboarding state from Stripe
        if (
            CurrentTenant.StripeOnboardingState != StripeOnboardingState.NotStarted &&
            CurrentTenant.StripeOnboardingState != StripeOnboardingState.Completed
        )
        {
            try
            {
                // Update tenant state based on current state in Stripe
                var tenant = await _tenantOnboardingService.RefreshStripeAccountAsync(
                    CurrentTenantInfo.Id
                );

                // Reload tenant to get updated onboarding state
                CurrentTenant = tenant;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error refreshing Stripe account for tenant {TenantId}",
                    CurrentTenantInfo.Id
                );
            }
        }

        // Fetch tenant statistics
        TenantStats.TotalMembers = await DbContext.TenantUsers
            .Where(tu => tu.TenantId == CurrentTenantInfo.Id)
            .CountAsync();

        TenantStats.ActiveEvents = await DbContext.Events
            .CountAsync(
                e => (e.StartTimeUtc >= utcNow || e.EndTimeUtc >= utcNow)
                    && e.IsActive
            );

        // Fetch the next 5 current or upcoming active events from the database
        var events = await DbContext.Events
            .Where(
                e => (e.StartTimeUtc >= utcNow || e.EndTimeUtc >= utcNow)
                    && e.IsActive
            )
            .Include(e => e.EventRegistrations)
            .OrderBy(e => e.StartTimeUtc)
            .Take(5)
            .ToListAsync();

        // Convert UTC times to local timezone for display
        UpcomingEvents = events.Select(e =>
        {
            var localStart = e.StartTimeUtc.ToTimeZone(e.TimeZoneId);
            var tzShort = e.TimeZoneId.GetAbbreviationFromUtc(e.StartTimeUtc);

            return new UpcomingEventDto
            {
                Id = e.Id,
                Name = e.Name,
                Date = localStart,
                Time = $"{localStart:h:mm tt} ({tzShort})",
                LocationDetails = e.LocationDetails,
                Registrations = e.EventRegistrations.Count(r =>
                    r.Status == EventRegistrationStatus.Registered ||
                    r.Status == EventRegistrationStatus.Attended
                ),
                IsHappening = utcNow >= e.StartTimeUtc && utcNow <= e.EndTimeUtc
            };
        }).ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostSetupStripeAsync()
    {
        try
        {
            // Get the logged-in admin user's email from claims
            var adminEmail = User?.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(adminEmail))
            {
                TenantAdminDashboardErrorMessage = "Unable to determine admin user email.";
                return RedirectToPage();
            }

            // Generate URLs for redirect
            var refreshUrl = Url.PageLink("/Tenant/Admin/Dashboard") ?? "/admin/dashboard";
            var returnUrl = Url.PageLink("/Tenant/Admin/Dashboard") ?? "/admin/dashboard";

            // Use onboarding service to set up Stripe Connect
            var onboardingUrl = await _tenantOnboardingService.SetupStripeConnectAsync(
                CurrentTenantInfo.Id,
                adminEmail,
                refreshUrl,
                returnUrl
            );

            // Redirect to Stripe onboarding
            return Redirect(onboardingUrl);
        }
        catch (InvalidOperationException ex)
        {
            // Handle business logic errors (duplicate account, tenant not found, etc.)
            _logger.LogError(
                ex,
                "Error setting up Stripe for tenant {TenantId}",
                CurrentTenantInfo.Id
            );
            TenantAdminDashboardErrorMessage = ex.Message;
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            // Log error and show generic message
            _logger.LogError(
                ex,
                "Unexpected error setting up Stripe for tenant {TenantId}",
                CurrentTenantInfo.Id
            );
            TenantAdminDashboardErrorMessage = $"Failed to set up Stripe Connect: {ex.Message}";
            return RedirectToPage();
        }
    }

    public async Task<IActionResult> OnPostContinueStripeSetupAsync()
    {
        try
        {
            // Generate URLs for redirect
            var refreshUrl = Url.PageLink("/Tenant/Admin/Dashboard") ?? "/admin/dashboard";
            var returnUrl = Url.PageLink("/Tenant/Admin/Dashboard") ?? "/admin/dashboard";

            // Use onboarding service to set up Stripe Connect
            var onboardingUrl = await _tenantOnboardingService.GetAccountUpdateLinkAsync(
                CurrentTenantInfo.Id,
                refreshUrl,
                returnUrl
            );

            // Redirect to Stripe onboarding
            return Redirect(onboardingUrl);
        }
        catch (InvalidOperationException ex)
        {
            // Handle business logic errors (duplicate account, tenant not found, etc.)
            _logger.LogError(
                ex,
                "Error continuing Stripe setup for tenant {TenantId}",
                CurrentTenantInfo.Id
            );
            TenantAdminDashboardErrorMessage = ex.Message;
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            // Log error and show generic message
            _logger.LogError(
                ex,
                "Unexpected error continuing Stripe setup for tenant {TenantId}",
                CurrentTenantInfo.Id
            );
            TenantAdminDashboardErrorMessage = $"Failed to continue Stripe Connect setup: {ex.Message}";
            return RedirectToPage();
        }
    }

    public async Task<IActionResult> OnPostRefreshStripeAccountAsync()
    {
        try
        {
            // Refresh Stripe account information
            var _ = await _tenantOnboardingService.RefreshStripeAccountAsync(
                CurrentTenantInfo.Id
            );

            // Redirect back to dashboard to show updated state
            return RedirectToPage();
        }
        catch (InvalidOperationException ex)
        {
            // Handle business logic errors
            _logger.LogError(
                ex,
                "Error refreshing Stripe account for tenant {TenantId}",
                CurrentTenantInfo.Id
            );
            TenantAdminDashboardErrorMessage = ex.Message;
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            // Log error and show generic message
            _logger.LogError(
                ex,
                "Unexpected error refreshing Stripe account for tenant {TenantId}",
                CurrentTenantInfo.Id
            );
            TenantAdminDashboardErrorMessage = $"Failed to refresh Stripe account: {ex.Message}";
            return RedirectToPage();
        }
    }
}

/// <summary>
/// DTO for displaying upcoming events on the dashboard.
/// </summary>
public class UpcomingEventDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Time { get; set; } = string.Empty;
    public string? LocationDetails { get; set; }
    public int Registrations { get; set; }
    public bool IsHappening { get; set; }
}

public class TenantStatsDto
{
    public int TotalMembers { get; set; }
    public int ActiveEvents { get; set; }
}
