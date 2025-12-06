using Microsoft.EntityFrameworkCore;
using ClubManagement.Infrastructure.Persistence;
using Finbuckle.MultiTenant.Abstractions;
using ClubManagement.Core.Entities;

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
        UpcomingEvents = await _dbContext.Events
            .Where(e => e.IsActive)
            .Include(e => e.EventRegistrations)
            .OrderBy(e => e.CreatedAt)
            .Take(5)
            .Select(e => new UpcomingEventDto
            {
                Id = e.Id,
                Name = e.Name,
                Date = e.CreatedAt.AddDays(1), // Placeholder: events are 1 day from creation
                Time = "6:00 PM",
                Location = "Club Location",
                Registrations = e.EventRegistrations.Count
            })
            .ToListAsync();
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
}

public class TenantStatsDto
{
    public int TotalMembers { get; set; }
    public int ActiveEvents { get; set; }
}
