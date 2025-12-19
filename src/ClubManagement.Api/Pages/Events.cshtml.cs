using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClubManagement.Infrastructure.Persistence;
using ClubManagement.Infrastructure.Services;
using Finbuckle.MultiTenant.Abstractions;
using ClubManagement.Api.Utils;

namespace ClubManagement.Api.Pages;

public class EventsModel : PublicPageModel
{
    public List<EventCardDto> UpcomingEvents { get; set; } = new();
    public bool HasMoreEvents { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public string TypeFilter { get; set; } = "all";
    
    [BindProperty(SupportsGet = true)]
    public string AvailabilityFilter { get; set; } = "all";
    
    [BindProperty(SupportsGet = true)]
    public int PageNum { get; set; } = 1;
    
    private const int PageSize = 5;
    
    public bool HasActiveFilters => TypeFilter != "all" || AvailabilityFilter != "all";

    public EventsModel(
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor,
        ITenantConfigService tenantConfigService,
        AppDbContext dbContext
    ) : base(multiTenantContextAccessor, tenantConfigService, dbContext)
    {}

    public async Task<IActionResult> OnGetAsync()
    {
        // Build query with filters
        var query = DbContext.Events
            .Include(e => e.EventRegistrations)
            .Where(e => e.TenantId == CurrentTenantInfo.Id 
                     && e.IsActive 
                     && e.StartTimeUtc > DateTime.UtcNow);
        
        // Apply event type filter
        if (TypeFilter != "all")
        {
            query = query.Where(e => e.EventType == TypeFilter);
        }
        
        // Get one extra to check if there are more pages
        var events = await query
            .OrderBy(e => e.StartTimeUtc)
            .Skip((PageNum - 1) * PageSize)
            .Take(PageSize + 1)
            .ToListAsync();
        
        HasMoreEvents = events.Count > PageSize;
        if (HasMoreEvents)
        {
            events = events.Take(PageSize).ToList();
        }

        // Convert to DTOs with local time
        var eventDtos = events.Select(e =>
        {
            var localStart = e.StartTimeUtc.ToTimeZone(e.TimeZoneId);
            var localEnd = e.EndTimeUtc.ToTimeZone(e.TimeZoneId);
            var tzShort = e.TimeZoneId.GetAbbreviationFromUtc(e.StartTimeUtc);
            var registrations = e.EventRegistrations?.Count(r => r.Status == Core.Constants.EventRegistrationStatus.Registered) ?? 0;

            return new EventCardDto
            {
                Id = e.Id,
                Name = e.Name,
                Description = e.Description,
                EventType = e.EventType,
                AccentColor = TenantConfig.Theme.PrimaryColor,
                StartTimeLocal = localStart,
                EndTimeLocal = localEnd,
                TimeZoneAbbreviation = tzShort,
                MaxAttendees = e.Capacity,
                CurrentAttendees = registrations
            };
        }).ToList();
        
        // Apply availability filter
        if (AvailabilityFilter == "available")
        {
            eventDtos = eventDtos.Where(e => !e.IsFull).ToList();
        }
        else if (AvailabilityFilter == "full")
        {
            eventDtos = eventDtos.Where(e => e.IsFull).ToList();
        }
        
        UpcomingEvents = eventDtos;

        return Page();
    }
    
    public async Task<IActionResult> OnGetLoadMoreAsync(int pageNum, string typeFilter = "all", string availabilityFilter = "all")
    {
        TypeFilter = typeFilter;
        AvailabilityFilter = availabilityFilter;
        PageNum = pageNum;
        
        // Build query with filters
        var query = DbContext.Events
            .Include(e => e.EventRegistrations)
            .Where(e => e.TenantId == CurrentTenantInfo.Id 
                     && e.IsActive 
                     && e.StartTimeUtc > DateTime.UtcNow);
        
        // Apply event type filter
        if (TypeFilter != "all")
        {
            query = query.Where(e => e.EventType == TypeFilter);
        }
        
        // Get one extra to check if there are more pages
        var events = await query
            .OrderBy(e => e.StartTimeUtc)
            .Skip((PageNum - 1) * PageSize)
            .Take(PageSize + 1)
            .ToListAsync();
        
        var hasMore = events.Count > PageSize;
        if (hasMore)
        {
            events = events.Take(PageSize).ToList();
        }

        // Convert to DTOs with local time
        var eventDtos = events.Select(e =>
        {
            var localStart = e.StartTimeUtc.ToTimeZone(e.TimeZoneId);
            var localEnd = e.EndTimeUtc.ToTimeZone(e.TimeZoneId);
            var registrations = e.EventRegistrations?.Count(r => r.Status == Core.Constants.EventRegistrationStatus.Registered) ?? 0;

            return new EventCardDto
            {
                Id = e.Id,
                Name = e.Name,
                Description = e.Description,
                EventType = e.EventType,
                AccentColor = TenantConfig.Theme.PrimaryColor,
                StartTimeLocal = localStart,
                EndTimeLocal = localEnd,
                MaxAttendees = e.Capacity,
                CurrentAttendees = registrations
            };
        }).ToList();
        
        // Apply availability filter
        if (AvailabilityFilter == "available")
        {
            eventDtos = eventDtos.Where(e => !e.IsFull).ToList();
        }
        else if (AvailabilityFilter == "full")
        {
            eventDtos = eventDtos.Where(e => e.IsFull).ToList();
        }
        
        return new JsonResult(new { events = eventDtos, hasMore });
    }
    
    public string BuildTypeUrl(string type) => $"/events?TypeFilter={type}&AvailabilityFilter={AvailabilityFilter}";
    public string BuildAvailabilityUrl(string availability) => $"/events?TypeFilter={TypeFilter}&AvailabilityFilter={availability}";
}

public class EventCardDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? AccentColor { get; set; }
    public DateTime StartTimeLocal { get; set; }
    public DateTime? EndTimeLocal { get; set; }
    public string TimeZoneAbbreviation { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int? MaxAttendees { get; set; }
    public int CurrentAttendees { get; set; }
    
    public bool IsFull => MaxAttendees.HasValue && CurrentAttendees >= MaxAttendees.Value;
    public bool IsSameDay => !EndTimeLocal.HasValue || StartTimeLocal.Date == EndTimeLocal.Value.Date;
}
