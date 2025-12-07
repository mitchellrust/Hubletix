using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ClubManagement.Core.Constants;
using ClubManagement.Core.Entities;
using ClubManagement.Infrastructure.Persistence;
using Finbuckle.MultiTenant.Abstractions;
using ClubManagement.Api.Utils;

namespace ClubManagement.Api.Pages.Admin;

public class EventDetailModel : TenantPageModel
{
    private readonly AppDbContext _dbContext;

    // Bind property so that form submission populates this object automatically based on property names
    [BindProperty]
    public Event? Event { get; set; }

    public List<SelectListItem> EventTypeOptions { get; set; } = new();
    public string? StatusMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public EventDetailModel(
        AppDbContext dbContext,
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor
    ) : base(multiTenantContextAccessor)
    {
        _dbContext = dbContext;
    }

    public async Task<IActionResult> OnGetAsync(string? id)
    {
        // ID wasn't provided, 404 not found feels right.
        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        Event = await _dbContext.Events
            .FirstOrDefaultAsync(e => e.Id == id);

        // If event not found, should return better UI than just blank page.
        if (Event == null)
        {
            return Page();
        }

        PopulateEventTypeOptions();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Event == null || string.IsNullOrEmpty(Event.Id))
        {
            return BadRequest();
        }

        // Verify event still exists in DB.
        var existingEvent = await _dbContext.Events
            .FirstOrDefaultAsync(e => e.Id == Event.Id);

        if (existingEvent == null)
        {
            return NotFound();
        }

        // Update allowed fields
        existingEvent.Name = Event.Name;
        existingEvent.Description = Event.Description;
        existingEvent.EventType = Event.EventType;
        existingEvent.Capacity = Event.Capacity;
        existingEvent.IsActive = Event.IsActive;

        try
        {
            _dbContext.Events.Update(existingEvent);
            await _dbContext.SaveChangesAsync();
            StatusMessage = "Event updated successfully.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error updating event: {ex.Message}";
        }

        PopulateEventTypeOptions();
        Event = existingEvent;
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return BadRequest();
        }

        // Verify event exists
        var eventToDelete = await _dbContext.Events
            .FirstOrDefaultAsync(e => e.Id == id);

        if (eventToDelete == null)
        {
            return NotFound();
        }

        try
        {
            _dbContext.Events.Remove(eventToDelete);
            await _dbContext.SaveChangesAsync();
            return RedirectToPage("/Admin/Events", new { message = "Event deleted successfully." });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error deleting event: {ex.Message}";
            Event = eventToDelete;
            PopulateEventTypeOptions();
            return Page();
        }
    }

    private void PopulateEventTypeOptions()
    {
        EventTypeOptions = Enum.GetValues(typeof(EventType))
            .Cast<EventType>()
            .Select(et => new SelectListItem
            {
                Value = et.ToString(),
                Text = et.ToString().Humanize(),
                Selected = Event?.EventType == et
            })
            .ToList();
    }
}
