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
                UserName = adminEmail,
                FirstName = adminFirstName,
                LastName = adminLastName,
                IsActive = true
            };

            _dbContext.Users.Add(adminUser);
            await _dbContext.SaveChangesAsync();

            // Create default membership plans
            await CreateDefaultMembershipPlansAsync(tenant.Id);

            // Create demo events
            await CreateDemoEventsAsync(tenant.Id, adminUser.Id);

            return tenant;
        }
        catch
        {
            // TODO: What should go here?
            throw;
        }
    }

    private async Task CreateDefaultMembershipPlansAsync(string tenantId)
    {
        var plans = new[]
        {
            new MembershipPlan
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                Name = "Monthly Membership",
                Description = "Full access to all classes and events",
                PriceInCents = 9999, // $99.99
                BillingInterval = "month",
                IsActive = true,
                DisplayOrder = 1
            },
            new MembershipPlan
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                Name = "Annual Membership",
                Description = "Full access for a full year (2 months free!)",
                PriceInCents = 99900, // $999.00
                BillingInterval = "year",
                IsActive = true,
                DisplayOrder = 2
            }
        };

        _dbContext.MembershipPlans.AddRange(plans);
        await _dbContext.SaveChangesAsync();
    }

    private async Task CreateDemoEventsAsync(string tenantId, string coachId)
    {
        var demoEvents = new[]
        {
            new Event
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                Name = "Morning CrossFit",
                Description = "High-intensity functional fitness training",
                EventType = Core.Constants.EventType.Class,
                CoachId = coachId,
                Capacity = 20,
                IsActive = true
            },
            new Event
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                Name = "Yoga & Flexibility",
                Description = "Low-intensity flexibility and mindfulness",
                EventType = Core.Constants.EventType.Class,
                CoachId = coachId,
                Capacity = 15,
                IsActive = true
            },
            new Event
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                Name = "Open Gym",
                Description = "Self-directed workout time",
                EventType = Core.Constants.EventType.Other,
                CoachId = null,
                Capacity = 50,
                IsActive = true
            }
        };

        _dbContext.Events.AddRange(demoEvents);
        await _dbContext.SaveChangesAsync();
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
