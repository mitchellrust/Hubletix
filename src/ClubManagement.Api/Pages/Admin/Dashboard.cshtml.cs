using Microsoft.EntityFrameworkCore;
using ClubManagement.Infrastructure.Persistence;
using Finbuckle.MultiTenant.Abstractions;
using ClubManagement.Api.Utils;
using ClubManagement.Infrastructure.Services;
using ClubManagement.Core.Constants;

namespace ClubManagement.Api.Pages.Admin;

public class DashboardModel : AdminPageModel
{
    public List<UpcomingEventDto> UpcomingEvents { get; set; } = new();
    public TenantStatsDto TenantStats { get; set; } = new();

    public DashboardModel(
        AppDbContext dbContext,
        ITenantConfigService tenantConfigService,
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor
    ) : base(
        multiTenantContextAccessor,
        tenantConfigService,
        dbContext
    )
    { }

    public async Task OnGetAsync()
    {
        var utcNow = DateTime.UtcNow;

        // Fetch tenant statistics
        TenantStats.TotalMembers = await DbContext.Users
            .Where(u => u.TenantId == CurrentTenantInfo.Id)
            .CountAsync();

        TenantStats.ActiveEvents = await DbContext.Events
            .CountAsync(
                e => (e.StartTimeUtc >= utcNow || e.EndTimeUtc >= utcNow)
                    && e.TenantId == CurrentTenantInfo.Id
                    && e.IsActive
            );

        // Fetch the next 5 current or upcoming active events from the database
        var events = await DbContext.Events
            .Where(
                e => (e.StartTimeUtc >= utcNow || e.EndTimeUtc >= utcNow)
                    && e.TenantId == CurrentTenantInfo.Id
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
