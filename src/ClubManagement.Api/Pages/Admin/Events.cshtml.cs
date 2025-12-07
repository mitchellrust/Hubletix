using Microsoft.EntityFrameworkCore;
using ClubManagement.Infrastructure.Persistence;
using Finbuckle.MultiTenant.Abstractions;
using ClubManagement.Core.Entities;

namespace ClubManagement.Api.Pages.Admin;

public class EventsModel : TenantPageModel
{
    private readonly AppDbContext _dbContext;
    public List<EventDto> Events { get; set; } = new();

    public EventsModel(
        AppDbContext dbContext,
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor
    ) : base(multiTenantContextAccessor)
    {
        _dbContext = dbContext;
    }

    public async Task OnGetAsync()
    {
        // Fetch the next events from the database
        Events = await _dbContext.Events
            .Where(e => e.IsActive)
            .Include(e => e.EventRegistrations)
            .OrderBy(e => e.CreatedAt)
            .Take(5)
            .Select(e => new EventDto
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
/// DTO for displaying events.
/// </summary>
public class EventDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Time { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int Registrations { get; set; }
}

