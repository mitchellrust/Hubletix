using Microsoft.EntityFrameworkCore;
using Finbuckle.MultiTenant.Abstractions;
using ClubManagement.Core.Entities;
using ClubManagement.Infrastructure.Persistence;
using ClubManagement.Core.Models;
using System.Text.Json;
namespace ClubManagement.Infrastructure.Services;

/// <summary>
/// Service for automated tenant onboarding.
/// Creates tenant record, admin user, default membership plans, and seeds demo data.
/// </summary>
public interface ITenantOnboardingService
{
    /// <summary>
    /// Creates a new tenant with admin user and default setup.
    /// </summary>
    Task<Tenant> OnboardTenantAsync(
        string name,
        string subdomain,
        string adminFirstName,
        string adminLastName,
        string adminEmail
    );
}

public class TenantOnboardingService : ITenantOnboardingService
{
    private readonly AppDbContext _dbContext;

    public TenantOnboardingService(
        AppDbContext dbContext,
        IMultiTenantContextAccessor multiTenantContextAccessor)
    {
        _dbContext = dbContext;
    }

    public async Task<Tenant> OnboardTenantAsync(
        string name,
        string subdomain,
        string adminFirstName,
        string adminLastName,
        string adminEmail
    )
    {
        // Check if subdomain already exists
        var existingTenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Subdomain == subdomain);
        
        if (existingTenant != null)
        {
            throw new InvalidOperationException($"Tenant with subdomain '{subdomain}' already exists.");
        }

        try
        {
             // Create tenant record
            var tenant = new Tenant
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Subdomain = subdomain,
                IsActive = true,
                ConfigJson = GetDefaultConfig()
            };

            _dbContext.Tenants.Add(tenant);
            await _dbContext.SaveChangesAsync();

            // Create admin user
            var adminUser = new User
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = tenant.Id,
                Email = adminEmail,
                FirstName = adminFirstName,
                LastName = adminLastName,
                IsActive = true
            };

            _dbContext.Users.Add(adminUser);
            await _dbContext.SaveChangesAsync();

            return tenant;
        }
        catch
        {
            // TODO: What should go here?
            throw;
        }
    }

    /// <summary>
    /// Get default configuration JSON string for demo tenant.
    /// </summary>
    private static string GetDefaultConfig()
    {
        return JsonSerializer.Serialize(
            new TenantConfig(),
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            }
        );
    }
}
