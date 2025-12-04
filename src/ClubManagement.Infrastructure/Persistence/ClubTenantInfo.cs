using Finbuckle.MultiTenant;

namespace ClubManagement.Infrastructure.Persistence;

/// <summary>
/// Finbuckle.MultiTenant TenantInfo implementation.
/// Represents the current tenant context for the request.
/// </summary>
public class ClubTenantInfo : ITenantInfo
{
    public string? Id { get; set; }
    public string? Identifier { get; set; }
    public string? Name { get; set; }
    public string? ConnectionString { get; set; }
    public object? Items { get; set; }
}
