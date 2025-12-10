using Microsoft.EntityFrameworkCore;
using ClubManagement.Infrastructure.Persistence;
using Finbuckle.MultiTenant.Abstractions;

namespace ClubManagement.Api.Pages.Admin;

public class MembersModel : TenantPageModel
{
    private readonly AppDbContext _dbContext;
    public List<MemberDto> Members { get; set; } = new();
    public int PageNum { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public string SortField { get; set; } = "name";
    private readonly string _defaultSortField = "name";
    public string SortDirection { get; set; } = "asc";
    private readonly string _sortDirectionDesc = "desc";
    private readonly string _sortDirectionAsc = "asc";
    public string? StatusFilter { get; set; } = "all"; // all, active, inactive
    public string? MembershipPlanFilter { get; set; }
    public List<MembershipPlanFacet> MembershipPlanFacets { get; set; } = new();
    public string? StatusMessage { get; set; }

    public MembersModel(
        AppDbContext dbContext,
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor
    ) : base(multiTenantContextAccessor)
    {
        _dbContext = dbContext;
    }

    public async Task OnGetAsync(string? sort = null, string? dir = null, int pageNum = 1, int pageSize = 10, string? status = null, string? plan = null, string? message = null)
    {
        // Capture status message from redirect
        StatusMessage = message;
        
        // Calculate pagination and sorting
        PageNum = Math.Max(1, pageNum);
        PageSize = Math.Clamp(pageSize, 5, 50);
        SortField = string.IsNullOrWhiteSpace(sort) ? _defaultSortField : sort.ToLowerInvariant();
        SortDirection = string.Equals(dir, _sortDirectionDesc, StringComparison.OrdinalIgnoreCase) ? _sortDirectionDesc : _sortDirectionAsc;
        StatusFilter = string.IsNullOrWhiteSpace(status) ? "all" : status.ToLowerInvariant();
        MembershipPlanFilter = plan;

        // Build a deferred query for users
        var query = _dbContext.Users
            .Select(u => new
                {
                    User = u,
                    MembershipPlanName = _dbContext.MembershipPlans
                        .Where(p => p.Id == u.MembershipPlanId)
                        .Select(p => p.Name)
                        .FirstOrDefault()
                }
            )
            .AsQueryable();

        // Apply status filter
        query = StatusFilter switch
        {
            "active" => query.Where(u => u.User.IsActive),
            "inactive" => query.Where(u => !u.User.IsActive),
            _ => query // "all" - no filter
        };
        
        // Apply membership plan filter
        if (!string.IsNullOrWhiteSpace(MembershipPlanFilter))
        {
            if (MembershipPlanFilter == "none")
            {
                query = query.Where(u => u.User.MembershipPlanId == null);
            }
            else
            {
                query = query.Where(u => u.User.MembershipPlanId == MembershipPlanFilter);
            }
        }

        // Get facet counts before applying pagination
        await LoadMembershipPlanFacetsAsync();

        // Apply sorting
        query = SortField switch
        {
            "email" => SortDirection == _sortDirectionAsc
                ? query.OrderBy(u => u.User.Email)
                : query.OrderByDescending(u => u.User.Email),
            "created" => SortDirection == _sortDirectionAsc
                ? query.OrderBy(u => u.User.CreatedAt)
                : query.OrderByDescending(u => u.User.CreatedAt),
            "membershipplan" => SortDirection == _sortDirectionAsc
                ? query.OrderBy(u => u.MembershipPlanName)
                : query.OrderByDescending(u => u.MembershipPlanName),
            _ => SortDirection == _sortDirectionAsc
                ? query.OrderBy(u => u.User.FirstName).ThenBy(u => u.User.LastName)
                : query.OrderByDescending(u => u.User.FirstName).ThenByDescending(u => u.User.LastName)
        };

        // Get total count for pagination
        var totalCount = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

        // Fetch paginated members (executes query)
        var users = await query
            .Skip((PageNum - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        // Project to DTO
        Members = users.Select(u => new MemberDto
        {
            Id = u.User.Id,
            FirstName = u.User.FirstName,
            LastName = u.User.LastName,
            FullName = $"{u.User.FirstName} {u.User.LastName}",
            Email = u.User.Email,
            IsActive = u.User.IsActive,
            MembershipPlanId = u.User.MembershipPlanId,
            JoinedDate = u.User.CreatedAt,
            MembershipPlanName = u.MembershipPlanName
        }).ToList();
    }
    
    private async Task LoadMembershipPlanFacetsAsync()
    {
        // Get all membership plans with member counts
        var planFacets = await _dbContext.MembershipPlans
            .OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.Name)
            .Select(p => new MembershipPlanFacet
            {
                Id = p.Id,
                Name = p.Name,
                Count = _dbContext.Users.Count(u => u.MembershipPlanId == p.Id)
            })
            .ToListAsync();
        
        // Add "No Plan" facet
        var noPlanCount = await _dbContext.Users.CountAsync(u => u.MembershipPlanId == null);
        planFacets.Add(new MembershipPlanFacet
        {
            Id = "none",
            Name = "No Plan",
            Count = noPlanCount
        });
        
        MembershipPlanFacets = planFacets;
    }
}

/// <summary>
/// DTO for membership plan facets.
/// </summary>
public class MembershipPlanFacet
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>
/// DTO for displaying members.
/// </summary>
public class MemberDto
{
    public string Id { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? MembershipPlanId { get; set; }
    public DateTime JoinedDate { get; set; }
    public string? MembershipPlanName { get; set; }
}
