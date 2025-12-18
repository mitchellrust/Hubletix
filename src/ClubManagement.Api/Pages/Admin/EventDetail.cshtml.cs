using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ClubManagement.Core.Constants;
using ClubManagement.Core.Entities;
using ClubManagement.Infrastructure.Persistence;
using Finbuckle.MultiTenant.Abstractions;
using ClubManagement.Api.Utils;
using ClubManagement.Api.Models;
using ClubManagement.Infrastructure.Services;

namespace ClubManagement.Api.Pages.Admin;

public class EventDetailModel : TenantPageModel
{
    // Bind property so that form submission populates this object automatically based on property names
    [BindProperty]
    public Event? Event { get; set; }

    [BindProperty]
    public string? LocalStartTime { get; set; }

    [BindProperty]
    public string? LocalEndTime { get; set; }

    public List<SelectListItem> EventTypeOptions { get; set; } = new();
    public List<SelectListItem> TimeZoneOptions { get; set; } = new();
    public string? StatusMessage { get; set; }
    public string? ErrorMessage { get; set; }
    
    // Registration properties
    public List<EventRegistrationDto> Registrations { get; set; } = new();
    public int PageNum { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public string SortField { get; set; } = "date";
    private readonly string _defaultSortField = "date";
    public string SortDirection { get; set; } = "desc";
    private readonly string _sortDirectionDesc = "desc";
    private readonly string _sortDirectionAsc = "asc";
    public string? RegistrationStatusFilter { get; set; } = "all";

    public EventDetailModel(
        AppDbContext dbContext,
        ITenantConfigService tenantConfigService,
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor
    ) : base(
        multiTenantContextAccessor,
        tenantConfigService,
        dbContext
    )
    { }

    public async Task<IActionResult> OnGetAsync(string? id, string? sort = null, string? dir = null, int pageNum = 1, int pageSize = 10, string? regStatus = null)
    {
        // ID wasn't provided, 404 not found feels right.
        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        Event = await DbContext.Events
            .FirstOrDefaultAsync(e => e.Id == id);

        // If event not found, should return better UI than 404.
        if (Event == null)
        {
            return Page();
        }

        // Convert UTC times to local times for form display
        var localStart = Event.StartTimeUtc.ToTimeZone(Event.TimeZoneId);
        var localEnd = Event.EndTimeUtc.ToTimeZone(Event.TimeZoneId);
        LocalStartTime = localStart.ToString("yyyy-MM-ddTHH:mm");
        LocalEndTime = localEnd.ToString("yyyy-MM-ddTHH:mm");

        PopulateEventTypeOptions();
        PopulateTimeZoneOptions();
        
        // Load registrations for this event
        await LoadEventRegistrationsAsync(id, sort, dir, pageNum, pageSize, regStatus);
        
        return Page();
    }
    
    private async Task LoadEventRegistrationsAsync(string eventId, string? sort, string? dir, int pageNum, int pageSize, string? regStatus)
    {
        // Calculate pagination and sorting
        PageNum = Math.Max(1, pageNum);
        PageSize = Math.Clamp(pageSize, 5, 50);
        SortField = string.IsNullOrWhiteSpace(sort) ? _defaultSortField : sort.ToLowerInvariant();
        SortDirection = string.Equals(dir, _sortDirectionDesc, StringComparison.OrdinalIgnoreCase) ? _sortDirectionDesc : _sortDirectionAsc;
        RegistrationStatusFilter = string.IsNullOrWhiteSpace(regStatus) ? "all" : regStatus.ToLowerInvariant();

        // Build query for registrations of this specific event
        var query = DbContext.EventRegistrations
            .Where(r => r.EventId == eventId)
            .Include(r => r.User)
            .Select(r => new
            {
                Registration = r,
                UserName = r.User.FirstName + " " + r.User.LastName
            })
            .AsQueryable();

        // Apply status filter
        if (RegistrationStatusFilter != "all")
        {
            query = query.Where(r => r.Registration.Status.ToLower() == RegistrationStatusFilter);
        }

        // Apply sorting
        query = SortField switch
        {
            "user" => SortDirection == _sortDirectionAsc
                ? query.OrderBy(r => r.UserName)
                : query.OrderByDescending(r => r.UserName),
            "status" => SortDirection == _sortDirectionAsc
                ? query.OrderBy(r => r.Registration.Status)
                : query.OrderByDescending(r => r.Registration.Status),
            _ => SortDirection == _sortDirectionAsc // date
                ? query.OrderBy(r => r.Registration.SignedUpAt)
                : query.OrderByDescending(r => r.Registration.SignedUpAt)
        };

        // Get total count for pagination
        var totalCount = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

        // Fetch paginated registrations
        var results = await query
            .Skip((PageNum - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        // Project to DTO
        Registrations = results.Select(r => new EventRegistrationDto
        {
            Id = r.Registration.Id,
            UserId = r.Registration.UserId,
            UserName = r.UserName,
            EventId = r.Registration.EventId,
            Status = r.Registration.Status,
            SignedUpAt = r.Registration.SignedUpAt,
            CancellationReason = r.Registration.CancellationReason
        }).ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Event == null || string.IsNullOrEmpty(Event.Id))
        {
            return BadRequest();
        }

        // Verify event still exists in DB.
        var existingEvent = await DbContext.Events
            .FirstOrDefaultAsync(e => e.Id == Event.Id);

        // If event not found, should return better UI than 404.
        if (existingEvent == null)
        {
            Event = null;
            return Page();
        }

        // Parse and convert local times to UTC
        DateTime startTimeUtc = existingEvent.StartTimeUtc;
        DateTime endTimeUtc = existingEvent.EndTimeUtc;
        
        if (!string.IsNullOrEmpty(LocalStartTime) && !string.IsNullOrEmpty(LocalEndTime))
        {
            if (DateTime.TryParse(LocalStartTime, out var localStart) && 
                DateTime.TryParse(LocalEndTime, out var localEnd))
            {
                // Validate start time is before or same as end time
                if (localStart > localEnd)
                {
                    ErrorMessage = "Start time must be before end time.";
                    PopulateEventTypeOptions();
                    PopulateTimeZoneOptions();
                    Event = existingEvent;
                    return Page();
                }

                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(Event.TimeZoneId);
                startTimeUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone);
                endTimeUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone);
            }
        }

        // Check if any fields have actually changed
        bool hasChanges = 
            existingEvent.Name != Event.Name ||
            existingEvent.Description != Event.Description ||
            existingEvent.EventType != Event.EventType ||
            existingEvent.Capacity != Event.Capacity ||
            existingEvent.IsActive != Event.IsActive ||
            existingEvent.TimeZoneId != Event.TimeZoneId ||
            existingEvent.StartTimeUtc != startTimeUtc ||
            existingEvent.EndTimeUtc != endTimeUtc;

        if (!hasChanges)
        {
            StatusMessage = "No changes were made.";
        }
        else
        {
            // Update allowed fields
            existingEvent.Name = Event.Name;
            existingEvent.Description = Event.Description;
            existingEvent.EventType = Event.EventType;
            existingEvent.Capacity = Event.Capacity;
            existingEvent.IsActive = Event.IsActive;
            existingEvent.TimeZoneId = Event.TimeZoneId;
            existingEvent.StartTimeUtc = startTimeUtc;
            existingEvent.EndTimeUtc = endTimeUtc;

            try
            {
                DbContext.Events.Update(existingEvent);
                await DbContext.SaveChangesAsync();
                StatusMessage = "Event updated successfully.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error updating event: {ex.Message}";
            }
        }

        // Repopulate form with current values
        PopulateEventTypeOptions();
        PopulateTimeZoneOptions();
        Event = existingEvent;
        
        // Convert UTC times back to local for display
        LocalStartTime = existingEvent.StartTimeUtc
            .ToTimeZone(existingEvent.TimeZoneId)
            .ToString("yyyy-MM-ddTHH:mm");
        LocalEndTime = existingEvent.EndTimeUtc
            .ToTimeZone(existingEvent.TimeZoneId)
            .ToString("yyyy-MM-ddTHH:mm");
        
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return BadRequest();
        }

        // Verify event exists
        var eventToDelete = await DbContext.Events
            .FirstOrDefaultAsync(e => e.Id == id);

        if (eventToDelete == null)
        {
            return RedirectToPage("/Admin/Events", new { message = "Event had already been deleted." });
        }

        try
        {
            DbContext.Events.Remove(eventToDelete);
            await DbContext.SaveChangesAsync();
            return RedirectToPage("/Admin/Events", new { message = "Event deleted successfully." });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error deleting event: {ex.Message}";
            Event = eventToDelete;
            PopulateEventTypeOptions();
            PopulateTimeZoneOptions();
            return Page();
        }
    }

    private void PopulateEventTypeOptions()
    {
        var eventTypeFields = typeof(EventType)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string));

        EventTypeOptions = eventTypeFields.Select(field =>
        {
            var value = field.GetValue(null)?.ToString() ?? "";
            var text = field.Name.Humanize();
            return new SelectListItem
            {
                Value = value,
                Text = text,
                Selected = Event?.EventType == value
            };
        }).ToList();
    }

    private void PopulateTimeZoneOptions(
        DateTime? startTimeUtc = null
    )
    {
        // US time zones
        var timeZones = new[]
        {
            "America/New_York",      // Eastern
            "America/Chicago",       // Central
            "America/Denver",        // Mountain
            "America/Los_Angeles",   // Pacific
            "America/Anchorage",     // Alaska
            "Pacific/Honolulu"       // Hawaii-Aleutian
        };

        TimeZoneOptions = timeZones.Select(tzId =>
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            var displayName = tzId.Replace("America/", "").Replace("Pacific/", "").Replace("_", " ");
            return new SelectListItem
            {
                Value = tzId,
                Text = $"{displayName} ({(startTimeUtc != null && tz.IsDaylightSavingTime(startTimeUtc.Value) ? tz.DaylightName : tz.StandardName)})",
                Selected = Event?.TimeZoneId == tzId
            };
        }).ToList();
    }
    
    public EventRegistrationsTableViewModel GetRegistrationsTableViewModel()
    {
        return new EventRegistrationsTableViewModel
        {
            Title = "Registrations",
            ContainerClass = "col-12 order-2 order-lg-3 my-5",
            EmptyMessage = "No registrations found for this event.",
            Registrations = Registrations,
            PageNum = PageNum,
            PageSize = PageSize,
            TotalPages = TotalPages,
            SortField = SortField,
            SortDirection = SortDirection,
            StatusFilter = RegistrationStatusFilter ?? "all",
            ShowEventColumn = false, // Don't show event column since we're already on an event page
            ShowFilterFacets = true,
            HasActiveFilters = !string.IsNullOrEmpty(RegistrationStatusFilter) && RegistrationStatusFilter != "all",
            PageName = $"/admin/events/{Event?.Id}",
            RouteValues = new Dictionary<string, string>
            {
                ["id"] = Event?.Id ?? string.Empty
            }
        };
    }
}
