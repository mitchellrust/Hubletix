using Finbuckle.MultiTenant.Abstractions;

namespace ClubManagement.Infrastructure.Persistence;

/// <summary>
/// Represents the current tenant context for the request.
/// </summary>
public record ClubTenantInfo(string Id, string Identifier, string Name) : TenantInfo(Id, Identifier, Name)
{
    public string? ConnectionString { get; set; }
    public object? Items { get; set; }
}
