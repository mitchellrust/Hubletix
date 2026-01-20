using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Hubletix.Infrastructure.Persistence;
using Finbuckle.MultiTenant.Abstractions;
using Hubletix.Api.Utils;
using Hubletix.Infrastructure.Services;
using Hubletix.Core.Constants;
using System.Security.Claims;
using Hubletix.Core.Enums;

namespace Hubletix.Api.Pages.Tenant.Admin;

public class DashboardModel : TenantAdminPageModel
{
    private readonly ITenantOnboardingService _tenantOnboardingService;
    private readonly ILogger<DashboardModel> _logger;
    private readonly ITenantConfigService _tenantConfigService;
    private readonly ICacheService _cacheService;
    
    public List<UpcomingEventDto> UpcomingEvents { get; set; } = new();
    public TenantStatsDto TenantStats { get; set; } = new();
    public Core.Entities.Tenant? CurrentTenant { get; set; }
    public DashboardFinancialMetrics FinancialMetrics { get; set; } = new();
    public MemberMetrics MemberMetrics { get; set; } = new();
    public RegistrationMetrics RegistrationMetrics { get; set; } = new();
    public EventMetrics EventMetrics { get; set; } = new();
    public List<RecentActivityDto> RecentActivities { get; set; } = new();

    [TempData]
    public string? TenantAdminDashboardErrorMessage { get; set; }

    public DashboardModel(
        AppDbContext dbContext,
        ITenantConfigService tenantConfigService,
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor,
        ITenantOnboardingService tenantOnboardingService,
        ICacheService cacheService,
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
        _cacheService = cacheService;
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
            .Where(
                tu => tu.TenantId == CurrentTenantInfo.Id &&
                      tu.Role == TenantRole.Member // We only want members, no coaches or admins
            )
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

        // Load all dashboard metrics with caching
        FinancialMetrics = await GetFinancialMetricsAsync();
        MemberMetrics = await GetMemberMetricsAsync();
        RegistrationMetrics = await GetRegistrationMetricsAsync();
        EventMetrics = await GetEventMetricsAsync();
        RecentActivities = await GetRecentActivitiesAsync();
        
        return Page();
    }

    private async Task<DashboardFinancialMetrics> GetFinancialMetricsAsync()
    {
        var cacheKey = $"dashboard:financial:{CurrentTenantInfo.Id}";
        return await _cacheService.GetOrSetAsync(cacheKey, async () =>
        {
            var metrics = new DashboardFinancialMetrics();
            var utcNow = DateTime.UtcNow;
            var firstDayOfMonth = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var firstDayOfNextMonth = firstDayOfMonth.AddMonths(1);
            var firstDayOfPreviousMonth = firstDayOfMonth.AddMonths(-1);

            // Query all payments for financial calculations
            var allPayments = await DbContext.Payments
                .ToListAsync();

            var succeededPayments = allPayments.Where(p => p.Status == "succeeded").ToList();
            
            // Total revenue
            metrics.TotalRevenueInDollars = succeededPayments.Sum(p => p.PriceInDollars);
            
            // Monthly revenue (current month)
            metrics.MonthlyRevenueInDollars = succeededPayments
                .Where(p => p.CreatedAt >= firstDayOfMonth && p.CreatedAt < firstDayOfNextMonth)
                .Sum(p => p.PriceInDollars);
            
            // Previous month revenue
            metrics.PreviousMonthRevenueInDollars = succeededPayments
                .Where(p => p.CreatedAt >= firstDayOfPreviousMonth && p.CreatedAt < firstDayOfMonth)
                .Sum(p => p.PriceInDollars);
            
            // Payment success rate
            var totalPayments = allPayments.Count;
            metrics.PaymentSuccessRate = totalPayments > 0 
                ? (decimal)succeededPayments.Count / totalPayments * 100 
                : 0;
            
            // Recent transactions - simplified without EventRegistration navigation
            metrics.RecentTransactions = await DbContext.Payments
                .Include(p => p.PlatformUser)
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .Select(p => new RecentTransactionDto
                {
                    Id = p.Id,
                    AmountInDollars = p.PriceInDollars,
                    Status = p.Status,
                    CreatedAt = p.CreatedAt,
                    EventName = p.Description,
                    UserName = p.PlatformUser != null ? p.PlatformUser.FullName : null
                })
                .ToListAsync();

            return metrics;
        }, absoluteExpiration: TimeSpan.FromMinutes(10)) ?? new DashboardFinancialMetrics();
    }

    private async Task<MemberMetrics> GetMemberMetricsAsync()
    {
        var cacheKey = $"dashboard:members:{CurrentTenantInfo.Id}";
        return await _cacheService.GetOrSetAsync(cacheKey, async () =>
        {
            var metrics = new MemberMetrics();
            var utcNow = DateTime.UtcNow;
            var thirtyDaysAgo = utcNow.AddDays(-30);

            var allMembers = await DbContext.TenantUsers
                .Where(
                    tu => tu.TenantId == CurrentTenantInfo.Id &&
                          tu.Role == TenantRole.Member // We only want members, no coaches or admins
                )
                .ToListAsync();

            // Member counts
            metrics.TotalMembers = allMembers.Count;
            metrics.ActiveMembers = allMembers.Count(m => m.Status == TenantUserStatus.Active);
            metrics.InactiveMembers = allMembers.Count(m => m.Status == TenantUserStatus.Inactive);
            metrics.PendingInvites = allMembers.Count(m => m.Status == TenantUserStatus.PendingInvite);
            metrics.NewMembersLast30Days = allMembers.Count(m => m.CreatedAt >= thirtyDaysAgo);

            // Member growth trend (last 30 days)
            metrics.MemberGrowthLast30Days = allMembers
                .Where(m => m.CreatedAt >= thirtyDaysAgo)
                .GroupBy(m => m.CreatedAt.Date)
                .Select(g => new DailyCountDto
                {
                    Date = g.Key,
                    Count = g.Count()
                })
                .OrderBy(d => d.Date)
                .ToList();

            return metrics;
        }, absoluteExpiration: TimeSpan.FromMinutes(10)) ?? new MemberMetrics();
    }

    private async Task<RegistrationMetrics> GetRegistrationMetricsAsync()
    {
        var cacheKey = $"dashboard:registrations:{CurrentTenantInfo.Id}";
        return await _cacheService.GetOrSetAsync(cacheKey, async () =>
        {
            var metrics = new RegistrationMetrics();
            var utcNow = DateTime.UtcNow;
            var thirtyDaysAgo = utcNow.AddDays(-30);

            var allRegistrations = await DbContext.EventRegistrations
                .Include(er => er.Event)
                .ToListAsync();

            // Total registrations
            metrics.TotalRegistrations = allRegistrations.Count;

            // Status breakdown
            metrics.StatusBreakdown = allRegistrations
                .GroupBy(r => r.Status)
                .ToDictionary(
                    g => g.Key.ToString(),
                    g => g.Count()
                );

            // Attendance rate (from past events only)
            var pastEventRegistrations = allRegistrations
                .Where(r => r.Event.EndTimeUtc < utcNow)
                .ToList();
            
            var attendedCount = pastEventRegistrations.Count(r => r.Status == EventRegistrationStatus.Attended);
            var registeredCount = pastEventRegistrations.Count(r => 
                r.Status == EventRegistrationStatus.Registered || 
                r.Status == EventRegistrationStatus.Attended);
            
            metrics.AttendanceRate = registeredCount > 0 
                ? (decimal)attendedCount / registeredCount * 100 
                : 0;

            // Cancellation rate
            var cancelledCount = allRegistrations.Count(r => r.Status == EventRegistrationStatus.Cancelled);
            metrics.CancellationRate = allRegistrations.Count > 0 
                ? (decimal)cancelledCount / allRegistrations.Count * 100 
                : 0;

            // Registration trend (last 30 days)
            metrics.RegistrationTrendLast30Days = allRegistrations
                .Where(r => r.CreatedAt >= thirtyDaysAgo)
                .GroupBy(r => r.CreatedAt.Date)
                .Select(g => new DailyCountDto
                {
                    Date = g.Key,
                    Count = g.Count()
                })
                .OrderBy(d => d.Date)
                .ToList();

            return metrics;
        }, absoluteExpiration: TimeSpan.FromMinutes(10)) ?? new RegistrationMetrics();
    }

    private async Task<EventMetrics> GetEventMetricsAsync()
    {
        var cacheKey = $"dashboard:events:{CurrentTenantInfo.Id}";
        return await _cacheService.GetOrSetAsync(cacheKey, async () =>
        {
            var metrics = new EventMetrics();
            var utcNow = DateTime.UtcNow;

            // Upcoming events capacity utilization
            var upcomingEvents = await DbContext.Events
                .Where(e => (e.StartTimeUtc >= utcNow || e.EndTimeUtc >= utcNow) && 
                            e.IsActive
                )
                .Include(e => e.EventRegistrations)
                .OrderBy(e => e.StartTimeUtc)
                .Take(10)
                .ToListAsync();

            metrics.UpcomingEventsCapacity = upcomingEvents
                .Select(e => new EventCapacityDto
                {
                    EventId = e.Id,
                    EventName = e.Name,
                    RegisteredCount = e.EventRegistrations.Count(r => 
                        r.Status == EventRegistrationStatus.Registered || 
                        r.Status == EventRegistrationStatus.Attended),
                    MaxCapacity = e.Capacity
                })
                .ToList();

            // Event type distribution
            var allEvents = await DbContext.Events
                .ToListAsync();

            metrics.EventTypeDistribution = allEvents
                .GroupBy(e => e.EventType)
                .ToDictionary(
                    g => g.Key.ToString(),
                    g => g.Count()
                );

            return metrics;
        }, absoluteExpiration: TimeSpan.FromMinutes(10)) ?? new EventMetrics();
    }

    private async Task<List<RecentActivityDto>> GetRecentActivitiesAsync()
    {
        var cacheKey = $"dashboard:activities:{CurrentTenantInfo.Id}";
        return await _cacheService.GetOrSetAsync(cacheKey, async () =>
        {
            var activities = new List<RecentActivityDto>();

            // Get recent registrations
            var recentRegistrations = await DbContext.EventRegistrations
                .Include(er => er.Event)
                .Include(er => er.PlatformUser)
                .Where(er => er.Event.TenantId == CurrentTenantInfo.Id)
                .OrderByDescending(er => er.CreatedAt)
                .Take(5)
                .Select(er => new RecentActivityDto
                {
                    Type = "registration",
                    Description = $"{er.PlatformUser.FullName} registered for {er.Event.Name}",
                    CreatedAt = er.CreatedAt,
                    Icon = "bi-calendar-check"
                })
                .ToListAsync();

            // Get recent members
            var recentMembers = await DbContext.TenantUsers
                .Where(tu => tu.TenantId == CurrentTenantInfo.Id)
                .Include(tu => tu.PlatformUser)
                .OrderByDescending(tu => tu.CreatedAt)
                .Take(5)
                .Select(tu => new RecentActivityDto
                {
                    Type = "member",
                    Description = $"{tu.PlatformUser.FullName} joined as {tu.Role}",
                    CreatedAt = tu.CreatedAt,
                    Icon = "bi-person-plus"
                })
                .ToListAsync();

            // Get recent payments
            var recentPayments = await DbContext.Payments
                .Include(p => p.PlatformUser)
                .Where(p => p.Status == "succeeded")
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .Select(p => new RecentActivityDto
                {
                    Type = "payment",
                    Description = p.PlatformUser != null 
                        ? $"{p.PlatformUser.FullName} paid ${p.PriceInDollars}"
                        : $"Payment of ${p.PriceInDollars} received",
                    CreatedAt = p.CreatedAt,
                    Icon = "bi-credit-card"
                })
                .ToListAsync();

            // Combine and sort all activities
            activities.AddRange(recentRegistrations);
            activities.AddRange(recentMembers);
            activities.AddRange(recentPayments);
            activities = activities.OrderByDescending(a => a.CreatedAt).Take(10).ToList();

            return activities;
        }, absoluteExpiration: TimeSpan.FromMinutes(5)) ?? new List<RecentActivityDto>();
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

public class DashboardFinancialMetrics
{
    public decimal TotalRevenueInDollars { get; set; }
    public decimal MonthlyRevenueInDollars { get; set; }
    public decimal PreviousMonthRevenueInDollars { get; set; }
    public decimal PaymentSuccessRate { get; set; }
    public List<RecentTransactionDto> RecentTransactions { get; set; } = new();
}

public class RecentTransactionDto
{
    public string Id { get; set; } = string.Empty;
    public decimal AmountInDollars { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? EventName { get; set; }
    public string? UserName { get; set; }
}

public class MemberMetrics
{
    public int TotalMembers { get; set; }
    public int ActiveMembers { get; set; }
    public int InactiveMembers { get; set; }
    public int PendingInvites { get; set; }
    public int NewMembersLast30Days { get; set; }
    public List<DailyCountDto> MemberGrowthLast30Days { get; set; } = new();
}

public class RegistrationMetrics
{
    public int TotalRegistrations { get; set; }
    public decimal AttendanceRate { get; set; }
    public decimal CancellationRate { get; set; }
    public Dictionary<string, int> StatusBreakdown { get; set; } = new();
    public List<DailyCountDto> RegistrationTrendLast30Days { get; set; } = new();
}

public class EventMetrics
{
    public List<EventCapacityDto> UpcomingEventsCapacity { get; set; } = new();
    public Dictionary<string, int> EventTypeDistribution { get; set; } = new();
}

public class DailyCountDto
{
    public DateTime Date { get; set; }
    public int Count { get; set; }
}

public class EventCapacityDto
{
    public string EventId { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public int RegisteredCount { get; set; }
    public int MaxCapacity { get; set; }
}

public class RecentActivityDto
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Icon { get; set; } = string.Empty;
}
