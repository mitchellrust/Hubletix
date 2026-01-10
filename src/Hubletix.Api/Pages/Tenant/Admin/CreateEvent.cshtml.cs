using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Hubletix.Core.Constants;
using Hubletix.Core.Entities;
using Hubletix.Infrastructure.Persistence;
using Finbuckle.MultiTenant.Abstractions;
using Hubletix.Api.Utils;
using Hubletix.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Hubletix.Api.Services;

namespace Hubletix.Api.Pages.Tenant.Admin;

public class CreateEventModel : AdminPageModel
{
    [BindProperty]
    public Event Event { get; set; } = new Event();

    [BindProperty]
    public string? LocalStartTime { get; set; }

    [BindProperty]
    public string? LocalEndTime { get; set; }

    [BindProperty]
    public string? LocalRegistrationDeadline { get; set; }

    [BindProperty]
    public decimal? PriceInDollars { get; set; }

    public List<SelectListItem> EventTypeOptions { get; set; } = new();
    public List<SelectListItem> TimeZoneOptions { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public CreateEventModel(
        AppDbContext dbContext,
        ITenantConfigService tenantConfigService,
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor,
        IUserContextService userContext
    ) : base(
        multiTenantContextAccessor,
        tenantConfigService,
        dbContext,
        userContext
    )
    { }

    public async Task<IActionResult> OnGetAsync()
    {
        Event.TimeZoneId = TenantConfig.Settings.TimeZoneId;
        Event.Capacity = 15;
        Event.EventType = EventType.Other;
        Event.IsActive = true;

        // Set default times based on tenant's timezone
        // TODO: Not sure if this the best, but setting to now seems reasonable
        var tzNow = DateTime.UtcNow.ToTimeZone(TenantConfig.Settings.TimeZoneId);
        LocalStartTime = tzNow.ToString("yyyy-MM-ddTHH:mm");
        LocalEndTime = tzNow.ToString("yyyy-MM-ddTHH:mm");

        PopulateEventTypeOptions();
        PopulateTimeZoneOptions();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(Event.Name))
        {
            ErrorMessage = "Event name is required.";
            PopulateEventTypeOptions();
            PopulateTimeZoneOptions();
            return Page();
        }

        // Parse and convert local times to UTC
        if (string.IsNullOrEmpty(LocalStartTime) || string.IsNullOrEmpty(LocalEndTime))
        {
            ErrorMessage = "Start time and end time are required.";
            PopulateEventTypeOptions();
            PopulateTimeZoneOptions();
            return Page();
        }

        if (!DateTime.TryParse(LocalStartTime, out var localStart) || 
            !DateTime.TryParse(LocalEndTime, out var localEnd))
        {
            ErrorMessage = "Invalid date/time format.";
            PopulateEventTypeOptions();
            PopulateTimeZoneOptions();
            return Page();
        }

        // Validate start time is not after end time
        if (localStart > localEnd)
        {
            ErrorMessage = "Start time must be before end time.";
            PopulateEventTypeOptions();
            PopulateTimeZoneOptions();
            return Page();
        }

        // Convert to UTC
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(Event.TimeZoneId);
        Event.StartTimeUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone);
        Event.EndTimeUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone);

        // Handle registration deadline if provided
        if (!string.IsNullOrEmpty(LocalRegistrationDeadline) && DateTime.TryParse(LocalRegistrationDeadline, out var localDeadline))
        {
            Event.RegistrationDeadlineUtc = TimeZoneInfo.ConvertTimeToUtc(localDeadline, timeZone);
        }
        else
        {
            Event.RegistrationDeadlineUtc = null;
        }

        // Handle price
        if (PriceInDollars.HasValue && PriceInDollars.Value >= 0)
        {
            Event.PriceInCents = (int)(PriceInDollars.Value * 100);
        }
        else
        {
            Event.PriceInCents = 0;
        }

        // Set tenant ID
        Event.TenantId = CurrentTenantInfo.Id;
        Event.Id = Guid.NewGuid().ToString();

        //TODO: Change to set location ID based on selection when multi-location is supported
        // For now, just using the default location for the tenant.
        var defaultLocationId = await DbContext.Locations
            .Where(l => l.IsDefault)
            .Select(l => l.Id)
            .FirstOrDefaultAsync();
        Event.LocationId = defaultLocationId ?? throw new Exception("Default location not found for tenant.");

        try
        {
            DbContext.Events.Add(Event);
            await DbContext.SaveChangesAsync();
            return RedirectToPage("/Admin/Events", new { id = Event.Id });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error creating event: {ex.Message}";
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

    private void PopulateTimeZoneOptions()
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
                Text = $"{displayName} ({tz.StandardName})",
                Selected = Event?.TimeZoneId == tzId
            };
        }).ToList();
    }
}
