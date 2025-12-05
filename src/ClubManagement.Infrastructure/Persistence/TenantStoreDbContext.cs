using Microsoft.EntityFrameworkCore;
using Finbuckle.MultiTenant.EntityFrameworkCore.Stores;

namespace ClubManagement.Infrastructure.Persistence;

/// <summary>
/// Standalone DbContext for Finbuckle.MultiTenant tenant store.
/// </summary>
public class TenantStoreDbContext : EFCoreStoreDbContext<ClubTenantInfo>
{
    public TenantStoreDbContext(
        DbContextOptions options
    ) : base(options)
    { }
}
