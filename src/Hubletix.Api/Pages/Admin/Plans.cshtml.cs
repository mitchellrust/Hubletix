using Microsoft.EntityFrameworkCore;
using ClubManagement.Infrastructure.Persistence;
using Finbuckle.MultiTenant.Abstractions;
using ClubManagement.Api.Utils;
using ClubManagement.Infrastructure.Services;

namespace ClubManagement.Api.Pages.Admin;

public class PlansModel : AdminPageModel
{
    public List<PlanDto> Plans { get; set; } = new();
    public int PageNum { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public string SortField { get; set; } = "order";
    private readonly string _defaultSortField = "order";
    public string SortDirection { get; set; } = "asc";
    private readonly string _sortDirectionDesc = "desc";
    private readonly string _sortDirectionAsc = "asc";
    public string? StatusFilter { get; set; } = "all"; // all, active, inactive
    public string? StatusMessage { get; set; }

    public PlansModel(
        AppDbContext dbContext,
        ITenantConfigService tenantConfigService,
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor
    ) : base(
        multiTenantContextAccessor,
        tenantConfigService,
        dbContext
    )
    { }

    public async Task OnGetAsync(string? sort = null, string? dir = null, int pageNum = 1, int pageSize = 10, string? status = null, string? message = null)
    {
        // Capture status message from redirect
        StatusMessage = message;
        
        // Calculate pagination and sorting
        PageNum = Math.Max(1, pageNum);
        PageSize = Math.Clamp(pageSize, 5, 50);
        SortField = string.IsNullOrWhiteSpace(sort) ? _defaultSortField : sort.ToLowerInvariant();
        SortDirection = string.Equals(dir, _sortDirectionDesc, StringComparison.OrdinalIgnoreCase) ? _sortDirectionDesc : _sortDirectionAsc;
        StatusFilter = string.IsNullOrWhiteSpace(status) ? "all" : status.ToLowerInvariant();

        // Build a deferred query for membership plans
        var query = DbContext.MembershipPlans
            .AsQueryable();

        // Apply status filter
        query = StatusFilter switch
        {
            "active" => query.Where(p => p.IsActive),
            "inactive" => query.Where(p => !p.IsActive),
            _ => query // "all" - no filter
        };

        // Apply sorting
        query = SortField switch
        {
            "name" => SortDirection == _sortDirectionAsc
                ? query.OrderBy(p => p.Name)
                : query.OrderByDescending(p => p.Name),
            "price" => SortDirection == _sortDirectionAsc
                ? query.OrderBy(p => p.PriceInCents)
                : query.OrderByDescending(p => p.PriceInCents),
            "interval" => SortDirection == _sortDirectionAsc
                ? query.OrderBy(p => p.BillingInterval)
                : query.OrderByDescending(p => p.BillingInterval),
            _ => SortDirection == _sortDirectionAsc
                ? query.OrderBy(p => p.DisplayOrder).ThenBy(p => p.Name)
                : query.OrderByDescending(p => p.DisplayOrder).ThenByDescending(p => p.Name)
        };

        // Get total count for pagination
        var totalCount = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

        // Fetch paginated plans (executes query)
        var plans = await query
            .Skip((PageNum - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        // Project to DTO
        Plans = plans.Select(p => new PlanDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.PriceInDollars.ToString("C"),
            BillingInterval = p.BillingInterval.Humanize(),
            IsActive = p.IsActive
        }).ToList();
    }
}

/// <summary>
/// DTO for displaying membership plans.
/// </summary>
public class PlanDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Price { get; set; } = string.Empty;
    public string BillingInterval { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
