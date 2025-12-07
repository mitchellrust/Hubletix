using Microsoft.EntityFrameworkCore;
using ClubManagement.Infrastructure.Persistence;
using Finbuckle.MultiTenant.Abstractions;
using ClubManagement.Api.Utils;

namespace ClubManagement.Api.Pages.Admin;

public class DashboardModel : TenantPageModel
{
    private readonly AppDbContext _dbContext;
    public List<UpcomingEventDto> UpcomingEvents { get; set; } = new();
    public TenantStatsDto TenantStats { get; set; } = new();

    public DashboardModel(
        AppDbContext dbContext,
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor
    ) : base(multiTenantContextAccessor)
    {
        _dbContext = dbContext;
    }

    public async Task OnGetAsync()
    {
        // Fetch tenant statistics
        TenantStats.TotalMembers = await _dbContext.Users
            .Where(u => u.TenantId == CurrentTenantInfo.Id)
            .CountAsync();

        TenantStats.ActiveEvents = await _dbContext.Events
            .Where(e => e.TenantId == CurrentTenantInfo.Id && e.IsActive)
            .CountAsync();

        // Fetch the next 5 active events from the database
        var events = await _dbContext.Events
            .Where(e => e.StartTimeUtc > DateTime.UtcNow)
            .Include(e => e.EventRegistrations)
            .OrderBy(e => e.StartTimeUtc)
            .Take(5)
            .ToListAsync();

        // Convert UTC times to local timezone for display
        UpcomingEvents = events.Select(e =>
        {
            var localStart = e.StartTimeUtc.ToTimeZone(e.TimeZoneId);
            var localEnd = e.EndTimeUtc.ToTimeZone(e.TimeZoneId);
            var tzShort = e.TimeZoneId.GetAbbreviationFromUtc(e.StartTimeUtc);

            return new UpcomingEventDto
            {
                Id = e.Id,
                Name = e.Name,
                Date = localStart,
                Time = $"{localStart:h:mm tt} - {localEnd:h:mm tt} ({tzShort})",
                Location = "Club", // TODO: Add location field to Event entity
                Registrations = e.EventRegistrations.Count,
                IsActive = e.IsActive
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
    public string Location { get; set; } = string.Empty;
    public int Registrations { get; set; }
    public bool IsActive { get; set; }
}

public class TenantStatsDto
{
    public int TotalMembers { get; set; }
    public int ActiveEvents { get; set; }
}
