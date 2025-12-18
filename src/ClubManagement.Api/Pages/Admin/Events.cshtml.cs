using Microsoft.EntityFrameworkCore;
using ClubManagement.Infrastructure.Persistence;
using Finbuckle.MultiTenant.Abstractions;
using ClubManagement.Api.Utils;
using ClubManagement.Api.Models;
using ClubManagement.Infrastructure.Services;

namespace ClubManagement.Api.Pages.Admin;

public class EventsModel : TenantPageModel
{
    private readonly AppDbContext _dbContext;
    public List<EventDto> Events { get; set; } = new();
    public int PageNum { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public string SortField { get; set; } = "date";
    private readonly string _defaultSortField = "date";
    public string SortDirection { get; set; } = "desc";
    private readonly string _sortDirectionDesc = "desc";
    private readonly string _sortDirectionAsc = "asc";
    public string? StatusFilter { get; set; } = "all"; // all, active, inactive
    public string? DateFilter { get; set; } = "upcoming"; // all, upcoming, past
    public string? StatusMessage { get; set; }

    public EventsModel(
        AppDbContext dbContext,
        ITenantConfigService tenantConfigService,
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor
    ) : base(
        multiTenantContextAccessor,
        tenantConfigService,
        dbContext
    )
    {
        _dbContext = dbContext;
    }

    public async Task OnGetAsync(string? sort = null, string? dir = null, int pageNum = 1, int pageSize = 10, string? status = null, string? date = null, string? message = null)
    {
        // Capture status message from redirect
        StatusMessage = message;
        
        // Calculate pagination and sorting
        PageNum = Math.Max(1, pageNum);
        PageSize = Math.Clamp(pageSize, 5, 50);
        SortField = string.IsNullOrWhiteSpace(sort) ? _defaultSortField : sort.ToLowerInvariant();
        SortDirection = string.Equals(dir, _sortDirectionDesc, StringComparison.OrdinalIgnoreCase) ? _sortDirectionDesc : _sortDirectionAsc;
        StatusFilter = string.IsNullOrWhiteSpace(status) ? "all" : status.ToLowerInvariant();
        DateFilter = string.IsNullOrWhiteSpace(date) ? "upcoming" : date.ToLowerInvariant();

        // Build a deferred query for events
        var query = _dbContext.Events
            .Include(e => e.EventRegistrations)
            .AsQueryable();

        // Apply status filter
        query = StatusFilter switch
        {
            "active" => query.Where(e => e.IsActive),
            "inactive" => query.Where(e => !e.IsActive),
            _ => query // "all" - no filter
        };

        // Apply date filter
        var nowUtc = DateTime.UtcNow;
        query = DateFilter switch
        {
            "upcoming" => query.Where(e => e.StartTimeUtc >= nowUtc),
            "past" => query.Where(e => e.StartTimeUtc < nowUtc),
            _ => query // "all" - no filter
        };

        // Apply sorting
        query = SortField switch
        {
            "name" => SortDirection == _sortDirectionAsc
                ? query.OrderBy(e => e.Name)
                : query.OrderByDescending(e => e.Name),
            "type" => SortDirection == _sortDirectionAsc
                ? query.OrderBy(e => e.EventType)
                : query.OrderByDescending(e => e.EventType),
            "capacity" => SortDirection == _sortDirectionAsc
                ? query.OrderBy(e => e.Capacity)
                : query.OrderByDescending(e => e.Capacity),
            "registrations" => SortDirection == _sortDirectionAsc
                ? query.OrderBy(e => e.EventRegistrations.Count)
                : query.OrderByDescending(e => e.EventRegistrations.Count),
            _ => SortDirection == _sortDirectionAsc
                ? query.OrderBy(e => e.StartTimeUtc)
                : query.OrderByDescending(e => e.StartTimeUtc)
        };

        // Get total count for pagination
        var totalCount = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

        // Fetch paginated events (executes query)
        var events = await query
            .Skip((PageNum - 1) * PageSize) // Skips items for previous pages
            .Take(PageSize)                 // Limits results to only the page size
            .ToListAsync();

        // Project to DTO in-memory
        Events = events.Select(e =>
        {
            var localStart = e.StartTimeUtc.ToTimeZone(e.TimeZoneId);
            var tzShort = e.TimeZoneId.GetAbbreviationFromUtc(e.StartTimeUtc);

            return new EventDto
            {
                Id = e.Id,
                Name = e.Name,
                Date = localStart,
                Time = $"{localStart:h:mm tt} ({tzShort})",
                Location = "Club Location",
                Registrations = e.EventRegistrations.Count,
                EventType = e.EventType.ToString().Humanize(),
                Capacity = e.Capacity,
                IsActive = e.IsActive
            };
        }).ToList();
    }
    
    public EventsTableViewModel GetEventsTableViewModel()
    {
        return new EventsTableViewModel
        {
            Title = "Events",
            ContainerClass = "",
            EmptyMessage = "No events found. Create your first event to get started.",
            Events = Events,
            PageNum = PageNum,
            PageSize = PageSize,
            TotalPages = TotalPages,
            SortField = SortField,
            SortDirection = SortDirection,
            StatusFilter = StatusFilter ?? "all",
            DateFilter = DateFilter ?? "upcoming",
            ShowFilterFacets = true,
            PageName = "/admin/events",
            RouteValues = new Dictionary<string, string>()
        };
    }
}

