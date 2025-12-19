using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClubManagement.Infrastructure.Persistence;
using ClubManagement.Infrastructure.Services;
using Finbuckle.MultiTenant.Abstractions;
using ClubManagement.Core.Entities;
using ClubManagement.Api.Utils;

namespace ClubManagement.Api.Pages;

public class EventsModel : PublicPageModel
{
    public List<EventCardDto> UpcomingEvents { get; set; } = new();

    public EventsModel(
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor,
        ITenantConfigService tenantConfigService,
        AppDbContext dbContext
    ) : base(multiTenantContextAccessor, tenantConfigService, dbContext)
    {}

    public async Task<IActionResult> OnGetAsync()
    {
        // Fetch upcoming active events
        var events = await DbContext.Events
            .Where(
              e => e.TenantId == CurrentTenantInfo.Id &&
              e.IsActive &&
              e.StartTimeUtc > DateTime.UtcNow
            )
            .OrderBy(e => e.StartTimeUtc)
            .ToListAsync();

        // Convert to DTOs with local time
        UpcomingEvents = events.Select(e =>
        {
            var localStart = e.StartTimeUtc.ToTimeZone(e.TimeZoneId);
            var localEnd = e.EndTimeUtc.ToTimeZone(e.TimeZoneId);
            var tzShort = e.TimeZoneId.GetAbbreviationFromUtc(e.StartTimeUtc);

            return new EventCardDto
            {
                Id = e.Id,
                Name = e.Name,
                Description = e.Description,
                AccentColor = TenantConfig.Theme.PrimaryColor,
                StartTimeLocal = localStart,
                EndTimeLocal = localEnd,
                TimeZoneAbbreviation = tzShort,
                MaxAttendees = e.Capacity,
                CurrentAttendees = e.EventRegistrations?.Count(r => r.Status == Core.Constants.EventRegistrationStatus.Registered) ?? 0
            };
        }).ToList();

        return Page();
    }
}

public class EventCardDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AccentColor { get; set; }
    public DateTime StartTimeLocal { get; set; }
    public DateTime? EndTimeLocal { get; set; }
    public string TimeZoneAbbreviation { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? ImageUrl { get; set; }
    public int? MaxAttendees { get; set; }
    public int CurrentAttendees { get; set; }
    
    public bool IsSoldOut => MaxAttendees.HasValue && CurrentAttendees >= MaxAttendees.Value;
    public bool IsSameDay => !EndTimeLocal.HasValue || StartTimeLocal.Date == EndTimeLocal.Value.Date;
}
