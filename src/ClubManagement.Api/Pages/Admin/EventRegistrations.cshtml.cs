using Microsoft.EntityFrameworkCore;
using ClubManagement.Infrastructure.Persistence;
using Finbuckle.MultiTenant.Abstractions;
using ClubManagement.Core.Constants;
using ClubManagement.Api.Models;

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
    public string? TimeFilter { get; set; } = "upcoming"; // all, upcoming, previous
    public List<EventFacet> EventFacets { get; set; } = new();
    public string? StatusMessage { get; set; }

    public EventRegistrationsModel(
        AppDbContext dbContext,
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor
    ) : base(multiTenantContextAccessor)
    {
        _dbContext = dbContext;
    }

    public async Task OnGetAsync(string? sort = null, string? dir = null, int pageNum = 1, int pageSize = 10, string? status = null, string? time = null, string? eventId = null, string? message = null)
    {
        StatusMessage = message;
        
        // Calculate pagination and sorting
        PageNum = Math.Max(1, pageNum);
        PageSize = Math.Clamp(pageSize, 5, 50);
        SortField = string.IsNullOrWhiteSpace(sort) ? _defaultSortField : sort.ToLowerInvariant();
        SortDirection = string.Equals(dir, _sortDirectionDesc, StringComparison.OrdinalIgnoreCase) ? _sortDirectionDesc : _sortDirectionAsc;
        StatusFilter = string.IsNullOrWhiteSpace(status) ? "all" : status.ToLowerInvariant();
        TimeFilter = string.IsNullOrWhiteSpace(time) ? "upcoming" : time.ToLowerInvariant();

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

        // Apply event time filter
        var nowUtc = DateTime.UtcNow;
        if (TimeFilter == "upcoming")
        {
            query = query.Where(r => r.EventStartTime >= nowUtc);
        }
        else if (TimeFilter == "previous")
        {
            query = query.Where(r => r.EventStartTime < nowUtc);
        }

        // Get facet counts
        await LoadEventFacetsAsync(nowUtc, StatusFilter, TimeFilter);

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
    
    private async Task LoadEventFacetsAsync(DateTime nowUtc, string? statusFilter, string? timeFilter)
    {
        // Build base query with status filter applied
        var registrationQuery = _dbContext.EventRegistrations.AsQueryable();
        
        if (statusFilter != "all")
        {
            registrationQuery = registrationQuery.Where(r => r.Status.ToLower() == statusFilter);
        }
        
        // Build event query with time filter
        var eventQuery = _dbContext.Events.AsQueryable();
        
        if (timeFilter == "upcoming")
        {
            eventQuery = eventQuery.Where(e => e.StartTimeUtc >= nowUtc);
        }
        else if (timeFilter == "previous")
        {
            eventQuery = eventQuery.Where(e => e.StartTimeUtc < nowUtc);
        }
        // "all" - no time filter
        
        // Get events with registration counts
        var events = await eventQuery
            .OrderByDescending(e => e.StartTimeUtc)
            .Select(e => new EventFacet
            {
                Id = e.Id,
                Name = e.Name,
                StartTime = e.StartTimeUtc,
                Count = registrationQuery.Count(r => r.EventId == e.Id)
            })
            .Take(20)
            .ToListAsync();
        
        EventFacets = events;
    }
    
    public EventRegistrationsTableViewModel GetRegistrationsTableViewModel()
    {
        return new EventRegistrationsTableViewModel
        {
            Title = "Event Registrations",
            ContainerClass = "row",
            EmptyMessage = "No registrations found.",
            Registrations = Registrations,
            PageNum = PageNum,
            PageSize = PageSize,
            TotalPages = TotalPages,
            SortField = SortField,
            SortDirection = SortDirection,
            StatusFilter = StatusFilter ?? "all",
            TimeFilter = TimeFilter,
            ShowEventColumn = true,
            ShowFilterFacets = true,
            HasActiveFilters = (!string.IsNullOrEmpty(StatusFilter) && StatusFilter != "all") || TimeFilter != "all",
            PageName = "/admin/event-registrations",
            RouteValues = new Dictionary<string, string>
            {
                ["time"] = TimeFilter ?? "all"
            }
        };
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
