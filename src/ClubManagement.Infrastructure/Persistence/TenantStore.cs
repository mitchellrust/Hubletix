using Finbuckle.MultiTenant.AspNetCore;
using Microsoft.EntityFrameworkCore;
using ClubManagement.Core.Entities;

namespace ClubManagement.Infrastructure.Persistence;

/// <summary>
/// Finbuckle ITenantStore implementation that loads tenants from PostgreSQL.
/// Queries tenant by Identifier (subdomain).
/// </summary>
public class TenantStore : IMultiTenantStore<ClubTenantInfo>
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public TenantStore(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<bool> TryAddAsync(ClubTenantInfo tenantInfo)
    {
        // We don't use this in our workflow - tenants are created via onboarding service
        throw new NotImplementedException();
    }

    public async Task<bool> TryRemoveAsync(string id)
    {
        // We don't use this in our workflow - tenants are deleted via admin service
        throw new NotImplementedException();
    }

    public async Task<bool> TryUpdateAsync(ClubTenantInfo tenantInfo)
    {
        // We don't use this in our workflow - tenants are updated via admin service
        throw new NotImplementedException();
    }

    public async Task<IMultiTenantStoreQueryResult<ClubTenantInfo>> TryGetAsync(string identifier)
    {
        try
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            
            // Query tenant by subdomain (identifier)
            var tenant = await dbContext.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Subdomain == identifier && t.IsActive);

            if (tenant == null)
            {
                return new MultiTenantStoreQueryResult<ClubTenantInfo>(false, null);
            }

            var tenantInfo = new ClubTenantInfo
            {
                Id = tenant.Id.ToString(),
                Identifier = tenant.Subdomain,
                Name = tenant.Name,
                // Store the Guid for later use in the DbContext
                Items = new Dictionary<string, object> { { "TenantGuid", tenant.Id } }
            };

            return new MultiTenantStoreQueryResult<ClubTenantInfo>(true, tenantInfo);
        }
        catch (Exception ex)
        {
            return new MultiTenantStoreQueryResult<ClubTenantInfo>(false, null, ex);
        }
    }

    public async Task<IEnumerable<ClubTenantInfo>> GetAllAsync()
    {
        try
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            
            var tenants = await dbContext.Tenants
                .AsNoTracking()
                .Where(t => t.IsActive)
                .ToListAsync();

            return tenants.Select(t => new ClubTenantInfo
            {
                Id = t.Id.ToString(),
                Identifier = t.Subdomain,
                Name = t.Name,
                Items = new Dictionary<string, object> { { "TenantGuid", t.Id } }
            }).ToList();
        }
        catch
        {
            return Enumerable.Empty<ClubTenantInfo>();
        }
    }
}
