using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClubManagement.Infrastructure.Persistence;
using ClubManagement.Infrastructure.Services;
using Finbuckle.MultiTenant.Abstractions;
using ClubManagement.Api.Utils;

namespace ClubManagement.Api.Pages;

public class EventDetailModel : PublicPageModel
{
    public EventDetailDto? Event { get; set; }

    public EventDetailModel(
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor,
        ITenantConfigService tenantConfigService,
        AppDbContext dbContext
    ) : base(multiTenantContextAccessor, tenantConfigService, dbContext)
    { }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var eventEntity = await DbContext.Events
            .Include(e => e.EventRegistrations)
            .FirstOrDefaultAsync(e => e.Id == id 
                                   && e.TenantId == CurrentTenantInfo.Id 
                                   && e.IsActive);

        if (eventEntity == null)
        {
            return NotFound();
        }

        var localStart = eventEntity.StartTimeUtc.ToTimeZone(eventEntity.TimeZoneId);
        var localEnd = eventEntity.EndTimeUtc.ToTimeZone(eventEntity.TimeZoneId);
        var tzShort = eventEntity.TimeZoneId.GetAbbreviationFromUtc(eventEntity.StartTimeUtc);
        var registrations = eventEntity.EventRegistrations?.Count(r => r.Status == Core.Constants.EventRegistrationStatus.Registered) ?? 0;

        Event = new EventDetailDto
        {
            Id = eventEntity.Id,
            Name = eventEntity.Name,
            Description = eventEntity.Description,
            EventType = eventEntity.EventType,
            AccentColor = TenantConfig.Theme.PrimaryColor,
            StartTimeLocal = localStart,
            EndTimeLocal = localEnd,
            TimeZoneAbbreviation = tzShort,
            MaxAttendees = eventEntity.Capacity,
            CurrentAttendees = registrations,
            Price = eventEntity.PriceInDollars,
            LocationDetails = eventEntity.LocationDetails,
            RegistrationDeadline = eventEntity.RegistrationDeadlineUtc?.ToTimeZone(eventEntity.TimeZoneId)
        };

        return Page();
    }
}

public class EventDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? LocationDetails { get; set; }
    public string? AccentColor { get; set; }
    public DateTime StartTimeLocal { get; set; }
    public DateTime? EndTimeLocal { get; set; }
    public string TimeZoneAbbreviation { get; set; } = string.Empty;
    public int? MaxAttendees { get; set; }
    public int CurrentAttendees { get; set; }
    public decimal? Price { get; set; }
    public DateTime? RegistrationDeadline { get; set; }
    
    public bool IsFull => MaxAttendees.HasValue && CurrentAttendees >= MaxAttendees.Value;
    public bool IsSameDay => !EndTimeLocal.HasValue || StartTimeLocal.Date == EndTimeLocal.Value.Date;
    public int SpotsRemaining => MaxAttendees.HasValue ? MaxAttendees.Value - CurrentAttendees : 0;
    public bool HasRegistrationDeadline => RegistrationDeadline.HasValue && RegistrationDeadline.Value > DateTime.Now;
    public bool IsRegistrationClosed => RegistrationDeadline.HasValue && RegistrationDeadline.Value <= DateTime.Now;
}
