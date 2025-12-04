using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Finbuckle.MultiTenant;
using ClubManagement.Core.Entities;
using ClubManagement.Infrastructure.Persistence;
using ClubManagement.Core.Constants;

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
    Task<Tenant> OnboardTenantAsync(string name, string subdomain, string adminEmail, string adminPassword);
}

public class TenantOnboardingService : ITenantOnboardingService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly IMultiTenantContext<ClubTenantInfo> _multiTenantContext;

    public TenantOnboardingService(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IMultiTenantContext<ClubTenantInfo> multiTenantContext)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _roleManager = roleManager;
        _multiTenantContext = multiTenantContext;
    }

    public async Task<Tenant> OnboardTenantAsync(string name, string subdomain, string adminEmail, string adminPassword)
    {
        // Check if subdomain already exists
        var existingTenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Subdomain == subdomain);
        
        if (existingTenant != null)
        {
            throw new InvalidOperationException($"Tenant with subdomain '{subdomain}' already exists.");
        }

        // Create tenant record
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name,
            Subdomain = subdomain,
            IsActive = true,
            ConfigJson = GetDefaultConfig()
        };

        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        // Manually set the tenant context for subsequent operations
        // Finbuckle will be bypassed for these operations since we're creating the tenant
        var tenantInfo = new ClubTenantInfo
        {
            Id = tenant.Id.ToString(),
            Identifier = tenant.Subdomain,
            Name = tenant.Name
        };
        _multiTenantContext.TenantInfo = tenantInfo;

        try
        {
            // Ensure roles exist (per-tenant role setup)
            await EnsureRolesExistAsync();

            // Create admin user
            var adminUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = name + " Admin",
                IsActive = true
            };

            var result = await _userManager.CreateAsync(adminUser, adminPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to create admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            // Assign Admin role
            await _userManager.AddToRoleAsync(adminUser, UserRoles.Admin);

            // Create default membership plans
            await CreateDefaultMembershipPlansAsync(tenant.Id);

            // Create demo events
            await CreateDemoEventsAsync(tenant.Id, adminUser.Id);

            return tenant;
        }
        catch
        {
            _multiTenantContext.TenantInfo = null;
            throw;
        }
    }

    private async Task EnsureRolesExistAsync()
    {
        var roles = new[] { UserRoles.Admin, UserRoles.Coach, UserRoles.Member };
        
        foreach (var roleName in roles)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                var role = new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = roleName,
                    NormalizedName = roleName.ToUpperInvariant()
                };
                await _roleManager.CreateAsync(role);
            }
        }
    }

    private async Task CreateDefaultMembershipPlansAsync(Guid tenantId)
    {
        var plans = new[]
        {
            new MembershipPlan
            {
                Id = Guid.NewGuid(),
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
                Id = Guid.NewGuid(),
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

    private async Task CreateDemoEventsAsync(Guid tenantId, Guid coachId)
    {
        var demEvents = new[]
        {
            new Event
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Morning CrossFit",
                Description = "High-intensity functional fitness training",
                EventType = "Class",
                CoachId = coachId,
                Capacity = 20,
                IsActive = true
            },
            new Event
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Yoga & Flexibility",
                Description = "Low-intensity flexibility and mindfulness",
                EventType = "Class",
                CoachId = coachId,
                Capacity = 15,
                IsActive = true
            },
            new Event
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Open Gym",
                Description = "Self-directed workout time",
                EventType = "Open Gym",
                CoachId = null,
                Capacity = 50,
                IsActive = true
            }
        };

        _dbContext.Events.AddRange(demEvents);
        await _dbContext.SaveChangesAsync();

        // Create demo schedules for each event
        var tomorrow = DateTime.UtcNow.AddDays(1);
        var schedules = new List<EventSchedule>();

        foreach (var evt in demEvents)
        {
            // Monday through Friday schedules
            for (int i = 0; i < 5; i++)
            {
                var scheduleDate = tomorrow.AddDays(i);
                var schedule = new EventSchedule
                {
                    Id = Guid.NewGuid(),
                    EventId = evt.Id,
                    DateTimeStart = scheduleDate.AddHours(9), // 9 AM
                    DateTimeEnd = scheduleDate.AddHours(10),   // 10 AM
                    Location = "Main Studio",
                    IsActive = true
                };
                schedules.Add(schedule);
            }
        }

        _dbContext.EventSchedules.AddRange(schedules);
        await _dbContext.SaveChangesAsync();
    }

    private static string GetDefaultConfig()
    {
        return """
        {
          "theme": {
            "primaryColor": "#4F46E5",
            "secondaryColor": "#06B6D4",
            "fontFamily": "Inter, sans-serif",
            "logoUrl": null
          },
          "features": {
            "enableMemberships": true,
            "enableEventSignups": true,
            "enablePayments": true
          },
          "settings": {
            "timezone": "America/Denver",
            "defaultCurrency": "usd"
          }
        }
        """;
    }
}
