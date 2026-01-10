using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Hubletix.Infrastructure.Persistence;
using Finbuckle.MultiTenant.Abstractions;
using Hubletix.Api.Utils;
using Hubletix.Infrastructure.Services;
using Hubletix.Core.Constants;

namespace Hubletix.Api.Pages.Admin;

public class DashboardModel : AdminPageModel
{
    private readonly ITenantOnboardingService _tenantOnboardingService;
    
    public List<UpcomingEventDto> UpcomingEvents { get; set; } = new();
    public TenantStatsDto TenantStats { get; set; } = new();

    public DashboardModel(
        AppDbContext dbContext,
        ITenantConfigService tenantConfigService,
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor,
        ITenantOnboardingService tenantOnboardingService
    ) : base(
        multiTenantContextAccessor,
        tenantConfigService,
        dbContext
    )
    {
        _tenantOnboardingService = tenantOnboardingService;
    }

    public async Task OnGetAsync()
    {
        var utcNow = DateTime.UtcNow;

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
    }

    public async Task<IActionResult> OnPostSetupStripeAsync()
    {
        try
        {
            // Generate URLs for redirect
            var refreshUrl = Url.PageLink("/Admin/Dashboard") ?? "/admin/dashboard";
            var returnUrl = Url.PageLink("/Admin/Dashboard") ?? "/admin/dashboard";
            
            // Use onboarding service to set up Stripe Connect
            var onboardingUrl = await _tenantOnboardingService.SetupStripeConnectAsync(
                CurrentTenantInfo.Id,
                "admin@example.com", // TODO: Use actual admin email
                refreshUrl,
                returnUrl
            );

            // Redirect to Stripe onboarding
            return Redirect(onboardingUrl);
        }
        catch (InvalidOperationException ex)
        {
            // Handle business logic errors (duplicate account, tenant not found, etc.)
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            // Log error and show generic message
            TempData["ErrorMessage"] = $"Failed to set up Stripe Connect: {ex.Message}";
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
