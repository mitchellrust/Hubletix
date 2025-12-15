using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ClubManagement.Infrastructure.Persistence;
using ClubManagement.Core.Entities;
using ClubManagement.Core.Models;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace ClubManagement.Infrastructure.Services;

/// <summary>
/// Service responsible for database initialization, migration application, and seeding.
/// Called on application startup to ensure database schema is up-to-date.
/// </summary>
public class DatabaseInitializationService
{
    private readonly ILogger<DatabaseInitializationService> _logger;

    public DatabaseInitializationService(ILogger<DatabaseInitializationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Initialize databases by applying migrations and seeding demo tenant if needed.
    /// </summary>
    public async Task InitializeDatabaseAsync(
      TenantStoreDbContext tenantStoreContext,
      AppDbContext appContext
    )
    {
        try
        {
          _logger.LogInformation("Starting database initialization...");

          // Apply TenantStore migrations
          _logger.LogInformation("Applying TenantStore migrations...");
          await tenantStoreContext.Database.MigrateAsync();
          _logger.LogInformation("TenantStore migrations applied successfully");

          // Apply App migrations
          _logger.LogInformation("Applying App database migrations...");
          await appContext.Database.MigrateAsync();
          _logger.LogInformation("App database migrations applied successfully");

          // Seed tenant store data
          ClubTenantInfo? demoTenantInfo = await SeedTenantStoreAsync(tenantStoreContext);

          if (demoTenantInfo != null)
          {
            // Seed application data with demo tenant info.
            await SeedAppAsync(appContext, demoTenantInfo);
          }

          _logger.LogInformation("Database initialization completed successfully");
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "An error occurred while initializing the database");
          throw;
        }
    }

    /// <summary>
    /// Seed tenant store with demo tenant data if needed.
    /// </summary>
    private async Task<ClubTenantInfo?> SeedTenantStoreAsync(TenantStoreDbContext context)
    {
        ClubTenantInfo? demoTenant = null;
        try
        {
            // Check if any tenants exist
            var tenantExists = await context.TenantInfo.AnyAsync();
            
            if (!tenantExists)
            {
                _logger.LogInformation("No tenants found. Creating demo tenant...");

                demoTenant = new ClubTenantInfo(
                    Id: Guid.NewGuid().ToString(),
                    Identifier: "demo",  // This is the subdomain
                    Name: "Demo VB Club"
                );

                context.TenantInfo.Add(demoTenant);
                await context.SaveChangesAsync();

                _logger.LogInformation("Demo tenant created with identifier: {Subdomain}", demoTenant.Identifier);
                _logger.LogInformation("Access the demo tenant at: https://{Subdomain}.localhost:5001", demoTenant.Identifier);
            }
            else
            {
                _logger.LogInformation("Tenant store already seeded");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding tenant store");
            throw;
        }

        return demoTenant;
    }

    /// <summary>
    /// Seed application database with demo tenant data if needed.
    /// This runs in a tenant-agnostic context.
    /// </summary>
    private async Task SeedAppAsync(
      AppDbContext context,
      ClubTenantInfo demoTenantInfo
    )
    {
        try
        {
            // Add any global seed data here (lookup tables, reference data, etc.)
            var demoTenant = new Tenant
            {
                Id = demoTenantInfo.Id,
                TenantId = demoTenantInfo.Id,
                Name = demoTenantInfo.Name!,
                Subdomain = demoTenantInfo.Identifier,
                IsActive = true,
                ConfigJson = GetDemoConfig()
            };

            var demoEvents = new List<Event>
            {
                new Event
                {
                    Id = Guid.NewGuid().ToString(),
                    TenantId = demoTenant.Id,
                    Name = "New Player Orientation",
                    Description = "An introductory event to welcome new members to the Demo VB Club.",
                    EventType = Core.Constants.EventType.GroupEvent,
                    Capacity = 200,
                    IsActive = true,
                    StartTimeUtc = DateTime.UtcNow.AddDays(7),
                    EndTimeUtc = DateTime.UtcNow.AddDays(7).AddHours(2),
                    TimeZoneId = "America/Denver",
                    CreatedBy = "System"
                },
                new Event
                {
                    Id = Guid.NewGuid().ToString(),
                    TenantId = demoTenant.Id,
                    Name = "Holiday Tournament",
                    Description = "Join us for our annual holiday volleyball tournament! Open to all skill levels.",
                    EventType = Core.Constants.EventType.Competition,
                    Capacity = 400,
                    IsActive = true,
                    StartTimeUtc = DateTime.UtcNow.AddDays(8),
                    EndTimeUtc = DateTime.UtcNow.AddDays(8).AddHours(2),
                    TimeZoneId = "America/Denver",
                    CreatedBy = "System"
                },
                new Event
                {
                    Id = Guid.NewGuid().ToString(),
                    TenantId = demoTenant.Id,
                    Name = "Parent & Me Volleyball",
                    Description = "A fun event for parents and their kids to play volleyball together.",
                    EventType = Core.Constants.EventType.GroupEvent,
                    Capacity = 50,
                    IsActive = true,
                    StartTimeUtc = DateTime.UtcNow.AddDays(3),
                    EndTimeUtc = DateTime.UtcNow.AddDays(3).AddHours(1),
                    TimeZoneId = "America/Denver",
                    CreatedBy = "System"
                }
            };

            var demoPlans = new List<MembershipPlan>
            {
                new MembershipPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    TenantId = demoTenant.Id,
                    Name = "Basic Membership",
                    Description = "Access to gym facilities during staffed hours.",
                    PriceInCents = 1000, // $10.00
                    BillingInterval = Core.Constants.BillingIntervals.Monthly,
                    IsPriceDisplayedMonthly = true,
                    IsActive = true,
                    DisplayOrder = 0,
                    CreatedBy = "System"
                },
                new MembershipPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    TenantId = demoTenant.Id,
                    Name = "Premium Membership",
                    Description = "Includes 24/7 gym access and discounts on events.",
                    PriceInCents = 12000, // $120.00, billed annually, displayed annually
                    BillingInterval = Core.Constants.BillingIntervals.Annually,
                    IsPriceDisplayedMonthly = false,
                    IsActive = true,
                    DisplayOrder = 1,
                    CreatedBy = "System"
                },
                new MembershipPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    TenantId = demoTenant.Id,
                    Name = "Ultra Membership",
                    Description = "Includes 24/7 gym access and discounts on events, AND MORE.",
                    PriceInCents = 15000, // $150.00 billed annually, displayed monthly
                    BillingInterval = Core.Constants.BillingIntervals.Annually,
                    IsPriceDisplayedMonthly = true,
                    IsActive = true,
                    DisplayOrder = 2,
                    CreatedBy = "System"
                },
                new MembershipPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    TenantId = demoTenant.Id,
                    Name = "One Time Pass",
                    Description = "A one time pass to the gym.",
                    PriceInCents = 700, // $7.00
                    BillingInterval = Core.Constants.BillingIntervals.OneTime,
                    IsPriceDisplayedMonthly = false,
                    IsActive = true,
                    DisplayOrder = 3,
                    CreatedBy = "System"
                }
            };

            var demoUsers = GenerateDemoUsers(demoTenant.Id, 100);
            demoUsers.ForEach(u =>
                {
                    if (u.IsActive)
                    {
                        // Add a membership plan to the user
                        u.MembershipPlanId = demoPlans[new Random().Next(demoPlans.Count)].Id;
                    }
                }
            );

            var demoEventRegistrations = new List<EventRegistration>
            {
                new EventRegistration
                {
                    Id = Guid.NewGuid().ToString(),
                    EventId = demoEvents[0].Id,
                    UserId = demoUsers[0].Id,
                    Status = Core.Constants.EventRegistrationStatus.Registered,
                    SignedUpAt = DateTime.UtcNow.AddDays(-1).AddHours(-2),
                    CreatedBy = "System"
                },
                new EventRegistration
                {
                    Id = Guid.NewGuid().ToString(),
                    EventId = demoEvents[1].Id,
                    UserId = demoUsers[0].Id,
                    Status = Core.Constants.EventRegistrationStatus.Attended,
                    SignedUpAt = DateTime.UtcNow.AddDays(-2).AddMinutes(-30),
                    CreatedBy = "System"
                },
                new EventRegistration
                {
                    Id = Guid.NewGuid().ToString(),
                    EventId = demoEvents[0].Id,
                    UserId = demoUsers[1].Id,
                    Status = Core.Constants.EventRegistrationStatus.Cancelled,
                    CancellationReason = "Scheduling conflict",
                    SignedUpAt = DateTime.UtcNow.AddDays(-10),
                    CreatedBy = "System"
                },
                new EventRegistration
                {
                    Id = Guid.NewGuid().ToString(),
                    EventId = demoEvents[0].Id,
                    UserId = demoUsers[2].Id,
                    Status = Core.Constants.EventRegistrationStatus.Registered,
                    SignedUpAt = DateTime.UtcNow.AddMinutes(-2),
                    CreatedBy = "System"
                },
                new EventRegistration
                {
                    Id = Guid.NewGuid().ToString(),
                    EventId = demoEvents[1].Id,
                    UserId = demoUsers[2].Id,
                    Status = Core.Constants.EventRegistrationStatus.Waitlist,
                    SignedUpAt = DateTime.UtcNow.AddMinutes(-2),
                    CreatedBy = "System"
                }
            };

            context.Tenants.Add(demoTenant);
            context.Events.AddRange(demoEvents);
            context.MembershipPlans.AddRange(demoPlans);
            context.Users.AddRange(demoUsers);
            context.EventRegistrations.AddRange(demoEventRegistrations);
            await context.SaveChangesAsync();

            _logger.LogInformation("Demo tenant created with identifier: {Subdomain}", demoTenant.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding app database");
            throw;
        }
    }

    /// <summary>
    /// Get default configuration JSON string for demo tenant.
    /// </summary>
    private static string GetDemoConfig()
    {
        var demoConfig = new TenantConfig
        {
            Settings = new SettingsConfig
            {
                TimeZoneId = "America/Denver",
                DefaultCurrency = "usd"
            },
            Theme = new ThemeConfig
            {
                PrimaryColor = "#4F46E5",
                SecondaryColor = "#06B6D4",
                LogoUrl = null
            },
            Features = new FeatureFlags
            {
                EnableMemberships = true,
                EnableEventRegistration = true,
                EnablePayments = true
            }
        };

        return JsonSerializer.Serialize(
            demoConfig,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            }
        );
    }

    /// <summary>
    /// Generate demo users with random names and emails.
    /// </summary>
    private static List<User> GenerateDemoUsers(string tenantId, int count)
    {
        var firstNames = new[] 
        { 
            "James", "Mary", "John", "Patricia", "Robert", "Jennifer", "Michael", "Linda", 
            "William", "Barbara", "David", "Elizabeth", "Richard", "Susan", "Joseph", "Jessica",
            "Thomas", "Sarah", "Charles", "Karen", "Christopher", "Nancy", "Daniel", "Lisa",
            "Matthew", "Betty", "Anthony", "Margaret", "Mark", "Sandra", "Donald", "Ashley",
            "Steven", "Kimberly", "Paul", "Emily", "Andrew", "Donna", "Joshua", "Michelle",
            "Kenneth", "Dorothy", "Kevin", "Carol", "Brian", "Amanda", "George", "Melissa",
            "Timothy", "Deborah", "Ronald", "Stephanie", "Edward", "Rebecca", "Jason", "Sharon",
            "Jeffrey", "Laura", "Ryan", "Cynthia", "Jacob", "Kathleen", "Gary", "Amy",
            "Nicholas", "Angela", "Eric", "Shirley", "Jonathan", "Anna", "Stephen", "Brenda"
        };
        
        var lastNames = new[] 
        { 
            "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis",
            "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas",
            "Taylor", "Moore", "Jackson", "Martin", "Lee", "Perez", "Thompson", "White",
            "Harris", "Sanchez", "Clark", "Ramirez", "Lewis", "Robinson", "Walker", "Young",
            "Allen", "King", "Wright", "Scott", "Torres", "Nguyen", "Hill", "Flores",
            "Green", "Adams", "Nelson", "Baker", "Hall", "Rivera", "Campbell", "Mitchell",
            "Carter", "Roberts", "Gomez", "Phillips", "Evans", "Turner", "Diaz", "Parker",
            "Cruz", "Edwards", "Collins", "Reyes", "Stewart", "Morris", "Morales", "Murphy"
        };

        var random = new Random(42); // Fixed seed for reproducibility
        var users = new List<User>();

        for (int i = 0; i < count; i++)
        {
            var firstName = firstNames[i % firstNames.Length];
            var lastName = lastNames[i % lastNames.Length];
            var email = $"{firstName.ToLower()}.{lastName.ToLower()}{(i > 50 ? i.ToString() : "")}@demo.com";
            
            users.Add(new User
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                IsActive = random.Next(100) > 10 // 90% active
            });
        }

        return users;
    }
}
