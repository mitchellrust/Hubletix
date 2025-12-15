using Microsoft.EntityFrameworkCore;
using ClubManagement.Infrastructure.Persistence;
using Finbuckle.MultiTenant.Abstractions;
using ClubManagement.Core.Constants;

namespace ClubManagement.Api.Pages.Admin;

public class EventRegistrationsModel : TenantPageModel
{
    private readonly AppDbContext _dbContext;
    public List<EventRegistrationDto> Registrations { get; set; } = new();
    public int PageNum { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public string SortField { get; set; } = "date";
    private readonly string _defaultSortField = "date";
    public string SortDirection { get; set; } = "desc";
    private readonly string _sortDirectionDesc = "desc";
    private readonly string _sortDirectionAsc = "asc";
    public string? StatusFilter { get; set; } = "all";
    public string? EventFilter { get; set; }
    public List<EventFacet> EventFacets { get; set; } = new();
    public string? StatusMessage { get; set; }

    public EventRegistrationsModel(
        AppDbContext dbContext,
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor
    ) : base(multiTenantContextAccessor)
    {
        _dbContext = dbContext;
    }

    public async Task OnGetAsync(string? sort = null, string? dir = null, int pageNum = 1, int pageSize = 10, string? status = null, string? eventId = null, string? message = null)
    {
        StatusMessage = message;
        
        // Calculate pagination and sorting
        PageNum = Math.Max(1, pageNum);
        PageSize = 2;
        SortField = string.IsNullOrWhiteSpace(sort) ? _defaultSortField : sort.ToLowerInvariant();
        SortDirection = string.Equals(dir, _sortDirectionDesc, StringComparison.OrdinalIgnoreCase) ? _sortDirectionDesc : _sortDirectionAsc;
        StatusFilter = string.IsNullOrWhiteSpace(status) ? "all" : status.ToLowerInvariant();
        EventFilter = eventId;

        // Build query
        var query = _dbContext.EventRegistrations
            .Include(r => r.User)
            .Include(r => r.Event)
            .Select(r => new
            {
                Registration = r,
                UserName = r.User.FirstName + " " + r.User.LastName,
                EventName = r.Event.Name,
                EventStartTime = r.Event.StartTimeUtc
            })
            .AsQueryable();

        // Apply status filter
        if (StatusFilter != "all")
        {
            query = query.Where(r => r.Registration.Status.ToLower() == StatusFilter);
        }
        
        // Apply event filter
        if (!string.IsNullOrWhiteSpace(EventFilter))
        {
            query = query.Where(r => r.Registration.EventId == EventFilter);
        }

        // Get facet counts
        await LoadEventFacetsAsync(StatusFilter);

        // Apply sorting
        query = SortField switch
        {
            "user" => SortDirection == _sortDirectionAsc
                ? query.OrderBy(r => r.UserName)
                : query.OrderByDescending(r => r.UserName),
            "event" => SortDirection == _sortDirectionAsc
                ? query.OrderBy(r => r.EventName)
                : query.OrderByDescending(r => r.EventName),
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
            EventName = r.EventName,
            EventStartTime = r.EventStartTime,
            Status = r.Registration.Status,
            SignedUpAt = r.Registration.SignedUpAt,
            CancellationReason = r.Registration.CancellationReason
        }).ToList();
    }
    
    private async Task LoadEventFacetsAsync(string? statusFilter)
    {
        // Build base query with status filter applied
        var registrationQuery = _dbContext.EventRegistrations.AsQueryable();
        
        if (statusFilter != "all")
        {
            registrationQuery = registrationQuery.Where(r => r.Status.ToLower() == statusFilter);
        }
        
        // Get upcoming events with registration counts
        var upcomingEvents = await _dbContext.Events
            .Where(e => e.StartTimeUtc >= DateTime.UtcNow)
            .OrderBy(e => e.StartTimeUtc)
            .Select(e => new EventFacet
            {
                Id = e.Id,
                Name = e.Name,
                StartTime = e.StartTimeUtc,
                Count = registrationQuery.Count(r => r.EventId == e.Id)
            })
            .Take(20)
            .ToListAsync();
        
        EventFacets = upcomingEvents;
    }
}

/// <summary>
/// DTO for event facets.
/// </summary>
public class EventFacet
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public int Count { get; set; }
}

/// <summary>
/// DTO for displaying event registrations.
/// </summary>
public class EventRegistrationDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public DateTime EventStartTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime SignedUpAt { get; set; }
    public string? CancellationReason { get; set; }
}
