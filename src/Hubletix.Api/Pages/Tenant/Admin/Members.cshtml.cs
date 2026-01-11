using Microsoft.EntityFrameworkCore;
using Hubletix.Infrastructure.Persistence;
using Finbuckle.MultiTenant.Abstractions;
using Hubletix.Infrastructure.Services;

namespace Hubletix.Api.Pages.Tenant.Admin;

public class MembersModel : AdminPageModel
{
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
        ITenantConfigService tenantConfigService,
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor
    ) : base(
        multiTenantContextAccessor,
        tenantConfigService,
        dbContext
    )
    { }

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
        var query = DbContext.TenantUsers
            .Where(u => u.TenantId == CurrentTenantInfo.Id)
            .Select(u => new
                {
                    TenantUserId = u.Id,
                    MembershipPlanId = u.MembershipPlanId,
                    PlatformUser = u.PlatformUser,
                    Email = u.PlatformUser.IdentityUser.Email,
                    MembershipPlanName = DbContext.MembershipPlans
                        .Where(p => p.Id == u.MembershipPlanId)
                        .Select(p => p.Name)
                        .FirstOrDefault()
                }
            );

        // Apply status filter
        query = StatusFilter switch
        {
            "active" => query.Where(u => u.PlatformUser.IsActive),
            "inactive" => query.Where(u => !u.PlatformUser.IsActive),
            _ => query // "all" - no filter
        };
        
        // Apply membership plan filter
        if (!string.IsNullOrWhiteSpace(MembershipPlanFilter))
        {
            if (MembershipPlanFilter == "none")
            {
                query = query.Where(u => u.MembershipPlanId == null);
            }
            else
            {
                query = query.Where(u => u.MembershipPlanId == MembershipPlanFilter);
            }
        }

        // Get facet counts before applying pagination
        await LoadMembershipPlanFacetsAsync();

        // Apply sorting
        query = SortField switch
        {
            "email" => SortDirection == _sortDirectionAsc
                ? query.OrderBy(u => u.Email)
                : query.OrderByDescending(u => u.Email),
            "membershipplan" => SortDirection == _sortDirectionAsc
                ? query.OrderBy(u => u.MembershipPlanName)
                : query.OrderByDescending(u => u.MembershipPlanName),
            _ => SortDirection == _sortDirectionAsc
                ? query.OrderBy(u => u.PlatformUser.FirstName).ThenBy(u => u.PlatformUser.LastName)
                : query.OrderByDescending(u => u.PlatformUser.FirstName).ThenByDescending(u => u.PlatformUser.LastName)
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
            Id = u.TenantUserId,
            FirstName = u.PlatformUser.FirstName,
            LastName = u.PlatformUser.LastName,
            FullName = u.PlatformUser.FullName,
            Email = u.Email!,
            IsActive = u.PlatformUser.IsActive,
            MembershipPlanId = u.MembershipPlanId,
            MembershipPlanName = u.MembershipPlanName
        }).ToList();
    }
    
    private async Task LoadMembershipPlanFacetsAsync()
    {
        // Build base query with status filter applied
        var userQuery = DbContext.TenantUsers
            .Include(tu => tu.PlatformUser)
            .Where(u => u.TenantId == CurrentTenantInfo.Id);
        
        if (StatusFilter == "active")
        {
            userQuery = userQuery.Where(u => u.PlatformUser.IsActive);
        }
        else if (StatusFilter == "inactive")
        {
            userQuery = userQuery.Where(u => !u.PlatformUser.IsActive);
        }
        // "all" or null - no filter applied
        
        // Get all membership plans with filtered member counts
        var planFacets = await DbContext.MembershipPlans
            .OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.Name)
            .Select(p => new MembershipPlanFacet
            {
                Id = p.Id,
                Name = p.Name,
                Count = userQuery.Count(u => u.MembershipPlanId == p.Id)
            })
            .ToListAsync();
        
        // Add "No Plan" facet with filtered count
        var noPlanCount = await userQuery.CountAsync(u => u.MembershipPlanId == null);
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
    public string? MembershipPlanName { get; set; }
}
