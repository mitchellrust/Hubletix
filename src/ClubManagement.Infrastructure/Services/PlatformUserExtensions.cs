using ClubManagement.Core.Entities;
using ClubManagement.Core.Enums;
using ClubManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClubManagement.Infrastructure.Services;

/// <summary>
/// Extension methods for querying PlatformUser and TenantUser relationships.
/// Provides convenient helper methods for authorization and multi-tenant queries.
/// </summary>
public static class PlatformUserExtensions
{
    /// <summary>
    /// Gets the TenantUser membership for a specific platform user in a specific tenant.
    /// Returns null if the user is not a member of the tenant.
    /// </summary>
    public static async Task<TenantUser?> GetTenantUserAsync(
        this AppDbContext dbContext,
        string platformUserId,
        string tenantId,
        CancellationToken ct = default)
    {
        return await dbContext.TenantUsers
            .Include(tu => tu.Tenant)
            .Include(tu => tu.PlatformUser)
            .FirstOrDefaultAsync(tu => 
                tu.PlatformUserId == platformUserId && 
                tu.TenantId == tenantId, 
                ct);
    }

    /// <summary>
    /// Gets all tenants that a platform user is a member of.
    /// Optionally filters by membership status.
    /// </summary>
    public static async Task<List<TenantUser>> GetUserTenantsAsync(
        this AppDbContext dbContext,
        string platformUserId,
        TenantUserStatus? statusFilter = null,
        CancellationToken ct = default)
    {
        var query = dbContext.TenantUsers
            .Include(tu => tu.Tenant)
            .Where(tu => tu.PlatformUserId == platformUserId);

        if (statusFilter.HasValue)
        {
            query = query.Where(tu => tu.Status == statusFilter.Value);
        }

        return await query
            .OrderByDescending(tu => tu.IsOwner)
            .ThenBy(tu => tu.Tenant.Name)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Checks if a platform user has a specific role (or higher) in a tenant.
    /// Role hierarchy: Admin > Coach > Member
    /// </summary>
    public static async Task<bool> HasRoleInTenantAsync(
        this AppDbContext dbContext,
        string platformUserId,
        string tenantId,
        TenantRole requiredRole,
        CancellationToken ct = default)
    {
        var tenantUser = await dbContext.TenantUsers
            .Where(tu => tu.PlatformUserId == platformUserId && tu.TenantId == tenantId)
            .Select(tu => new { tu.Role, tu.Status })
            .FirstOrDefaultAsync(ct);

        if (tenantUser == null || tenantUser.Status != TenantUserStatus.Active)
        {
            return false;
        }

        // Check role hierarchy
        return tenantUser.Role >= requiredRole;
    }

    /// <summary>
    /// Gets the PlatformUser from an IdentityUser ID.
    /// </summary>
    public static async Task<PlatformUser?> GetPlatformUserByIdentityIdAsync(
        this AppDbContext dbContext,
        string identityUserId,
        CancellationToken ct = default)
    {
        return await dbContext.PlatformUsers
            .Include(pu => pu.IdentityUser)
            .Include(pu => pu.DefaultTenant)
            .FirstOrDefaultAsync(pu => pu.IdentityUserId == identityUserId, ct);
    }

    /// <summary>
    /// Checks if a platform user is the owner of a specific tenant.
    /// </summary>
    public static async Task<bool> IsOwnerOfTenantAsync(
        this AppDbContext dbContext,
        string platformUserId,
        string tenantId,
        CancellationToken ct = default)
    {
        return await dbContext.TenantUsers
            .AnyAsync(tu => 
                tu.PlatformUserId == platformUserId && 
                tu.TenantId == tenantId && 
                tu.IsOwner, 
                ct);
    }

    /// <summary>
    /// Gets all active members of a tenant with their platform user information.
    /// Useful for member listing pages.
    /// </summary>
    public static async Task<List<TenantUser>> GetTenantMembersAsync(
        this AppDbContext dbContext,
        string tenantId,
        TenantUserStatus? statusFilter = null,
        CancellationToken ct = default)
    {
        var query = dbContext.TenantUsers
            .Include(tu => tu.PlatformUser)
                .ThenInclude(pu => pu.IdentityUser)
            .Where(tu => tu.TenantId == tenantId);

        if (statusFilter.HasValue)
        {
            query = query.Where(tu => tu.Status == statusFilter.Value);
        }

        return await query
            .OrderByDescending(tu => tu.IsOwner)
            .ThenByDescending(tu => tu.Role)
            .ThenBy(tu => tu.PlatformUser.FirstName)
            .ToListAsync(ct);
    }
}
