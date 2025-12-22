using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ClubManagement.Infrastructure.Persistence;
using ClubManagement.Core.Entities;
using ClubManagement.Core.Models;
using System.Text.Json;

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

          // Seed tenant store data with demo tenant if needed
          ClubTenantInfo? demoTenantInfo = await SeedTenantStoreAsync(
            tenantStoreContext,
            "demo"
          );

          if (demoTenantInfo != null)
          {
            // Seed application data with demo tenant info.
            await SeedAppDemoAsync(appContext, demoTenantInfo);
          }

          // Seed tenant store data with basic tenant info if needed
          ClubTenantInfo? basicTenantInfo = await SeedTenantStoreAsync(
            tenantStoreContext,
            "basic"
          );

          if (basicTenantInfo != null)
          {
            // Seed application data with basic tenant info.
            await SeedAppBasicAsync(appContext, basicTenantInfo);
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
    /// Seed tenant store with tenant data if needed.
    /// </summary>
    private async Task<ClubTenantInfo?> SeedTenantStoreAsync(
        TenantStoreDbContext context,
        string tenantIdentifier
    )
    {
        ClubTenantInfo? tenant = null;
        try
        {
            // Check if tenant exists
            var tenantExists = await context.TenantInfo.AnyAsync(t => t.Identifier == tenantIdentifier);
            
            if (!tenantExists)
            {
                _logger.LogInformation("No tenants found. Creating tenant...");

                tenant = new ClubTenantInfo(
                    Id: Guid.NewGuid().ToString(),
                    Identifier: tenantIdentifier,  // This is the subdomain
                    Name: $"{tenantIdentifier} VB Club"
                );

                context.TenantInfo.Add(tenant);
                await context.SaveChangesAsync();

                _logger.LogInformation("Tenant created with identifier: {Subdomain}", tenant.Identifier);
                _logger.LogInformation("Access the tenant at: https://{Subdomain}.localhost:5001", tenant.Identifier);
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

        return tenant;
    }

    /// <summary>
    /// Seed application database with demo tenant data if needed.
    /// This runs in a tenant-agnostic context.
    /// </summary>
    private async Task SeedAppBasicAsync(
      AppDbContext context,
      ClubTenantInfo basicTenantInfo
    )
    {
        try
        {
            // Add any global seed data here (lookup tables, reference data, etc.)
            var basicTenant = new Tenant
            {
                Id = basicTenantInfo.Id,
                TenantId = basicTenantInfo.Id,
                Name = basicTenantInfo.Name!,
                Subdomain = basicTenantInfo.Identifier,
                IsActive = true,
                ConfigJson = GetBasicConfig()
            };

            context.Tenants.Add(basicTenant);
            await context.SaveChangesAsync();

            _logger.LogInformation("Basic tenant created with identifier: {Subdomain}", basicTenant.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding app database");
            throw;
        }
    }

    /// <summary>
    /// Seed application database with demo tenant data if needed.
    /// This runs in a tenant-agnostic context.
    /// </summary>
    private async Task SeedAppDemoAsync(
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
                ConfigJson = GetDemoConfig(),
                Locations = new List<Location>()
                {
                    new Location
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "Main Gym",
                        Address = "456 Club St.",
                        City = "Denver",
                        State = "CO",
                        PostalCode = "80202",
                        Country = "USA",
                        PhoneNumber = "555-123-4567",
                        Email = "location@demo.com",
                        IsDefault = true,
                    }
                }
            };
            var basicTenant = new Tenant
            {
                Id = demoTenantInfo.Id,
                TenantId = demoTenantInfo.Id,
                Name = demoTenantInfo.Name!,
                Subdomain = demoTenantInfo.Identifier,
                IsActive = true,
                ConfigJson = GetDemoConfig(),
                Locations = new List<Location>()
                {
                    new Location
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "Default",
                        IsDefault = true
                    }
                }
            };

            var demoPlans = new List<MembershipPlan>
            {
                new MembershipPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    TenantId = demoTenant.Id,
                    LocationId = demoTenant.Locations.First().Id,
                    Name = "Basic Membership",
                    Description = "Gym access during staffed hours.",
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
                    LocationId = demoTenant.Locations.First().Id,
                    Name = "Premium Membership",
                    Description = "24/7 gym access.|Member-only events.",
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
                    LocationId = demoTenant.Locations.First().Id,
                    Name = "Ultra Membership",
                    Description = "24/7 gym access.|Member-only events.|Free guest passes.",
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
                    LocationId = demoTenant.Locations.First().Id,
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

            var demoUsers = GenerateDemoUsers(demoTenant.Id, demoTenant.Locations.First().Id, 100);
            demoUsers.ForEach(u =>
                {
                    if (u.IsActive)
                    {
                        // Add a membership plan to the user
                        u.MembershipPlanId = demoPlans[new Random().Next(demoPlans.Count)].Id;
                    }
                }
            );

            // Get Timezone info set up for event dates
            var tzString = "America/Denver";
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(tzString);
            var todayDateInTz = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;

            var demoEvents = new List<Event>
            {
                new Event
                {
                    Id = Guid.NewGuid().ToString(),
                    TenantId = demoTenant.Id,
                    LocationId = demoTenant.Locations.First().Id,
                    Name = "Free Event No Desc/Loc No Reg",
                    EventType = Core.Constants.EventType.Other,
                    Capacity = 10,
                    IsActive = true,
                    PriceInCents = 0,
                    StartTimeUtc = todayDateInTz.AddDays(7).AddHours(10).ToUniversalTime(),
                    EndTimeUtc = todayDateInTz.AddDays(7).AddHours(13).ToUniversalTime(),
                    TimeZoneId = tzString,
                    CreatedBy = "System",
                },
                new Event
                {
                    Id = "12345678-aaaa-bbbb-cccc-1234567890ab",
                    TenantId = demoTenant.Id,
                    LocationId = demoTenant.Locations.First().Id,
                    Name = "Multi-Day Tournament Some Reg",
                    Description = "Join us for our annual holiday volleyball tournament! Open to all skill levels.",
                    EventType = Core.Constants.EventType.Tournament,
                    PriceInCents = 2500, // $25.00
                    LocationDetails = "TVAC Gym",
                    Capacity = 5,
                    RegistrationDeadlineUtc = todayDateInTz.AddDays(8).AddMinutes(-1).ToUniversalTime(), // 11:59 the night before
                    IsActive = true,
                    StartTimeUtc = todayDateInTz.AddDays(8).AddHours(9).ToUniversalTime(),
                    EndTimeUtc = todayDateInTz.AddDays(9).AddHours(17).ToUniversalTime(),
                    TimeZoneId = tzString,
                    CreatedBy = "System",

                    EventRegistrations = new List<EventRegistration>
                    {
                        new EventRegistration
                        {
                            Id = Guid.NewGuid().ToString(),
                            EventId = "12345678-aaaa-bbbb-cccc-1234567890ab",
                            UserId = demoUsers[0].Id,
                            Status = Core.Constants.EventRegistrationStatus.Registered,
                            SignedUpAt = todayDateInTz.AddDays(-1).AddHours(-2).ToUniversalTime(),
                            CreatedBy = "System"
                        },
                        new EventRegistration
                        {
                            Id = Guid.NewGuid().ToString(),
                            EventId = "12345678-aaaa-bbbb-cccc-1234567890ab",
                            UserId = demoUsers[2].Id,
                            Status = Core.Constants.EventRegistrationStatus.Registered,
                            SignedUpAt = todayDateInTz.AddHours(-5).ToUniversalTime(),
                            CreatedBy = "System"
                        },
                        new EventRegistration
                        {
                            Id = Guid.NewGuid().ToString(),
                            EventId = "12345678-aaaa-bbbb-cccc-1234567890ab",
                            UserId = demoUsers[3].Id,
                            Status = Core.Constants.EventRegistrationStatus.Cancelled,
                            CancellationReason = "Can't make it",
                            SignedUpAt = todayDateInTz.AddHours(-5).ToUniversalTime(),
                            CreatedBy = "System"
                        }
                    }
                },
                new Event
                {
                    Id = "87654321-bbbb-cccc-dddd-0987654321ba",
                    TenantId = demoTenant.Id,
                    LocationId = demoTenant.Locations.First().Id,
                    Name = "Event Full Capacity",
                    Description = "A fun event for parents and their kids to play volleyball together.",
                    EventType = Core.Constants.EventType.Social,
                    LocationDetails = "123 Piper Lane, Meridian, ID 83646",
                    Capacity = 1, // Small capacity to demonstrate full event
                    IsActive = true,
                    StartTimeUtc = todayDateInTz.AddDays(3).AddHours(14).AddMinutes(30).ToUniversalTime(),
                    EndTimeUtc = todayDateInTz.AddDays(3).AddHours(15).AddMinutes(30).ToUniversalTime(),
                    TimeZoneId = tzString,
                    CreatedBy = "System",
                    EventRegistrations = new List<EventRegistration>
                    {
                        new EventRegistration
                        {
                            Id = Guid.NewGuid().ToString(),
                            EventId = "87654321-bbbb-cccc-dddd-0987654321ba",
                            UserId = demoUsers[0].Id,
                            Status = Core.Constants.EventRegistrationStatus.Registered,
                            SignedUpAt = todayDateInTz.AddHours(-1).ToUniversalTime(),
                            CreatedBy = "System"
                        }
                    }
                },
                new Event
                {
                    Id = Guid.NewGuid().ToString(),
                    TenantId = demoTenant.Id,
                    LocationId = demoTenant.Locations.First().Id,
                    Name = "Inactive Event",
                    Description = "An event that is inactive.",
                    EventType = Core.Constants.EventType.Social,
                    Capacity = 5,
                    LocationDetails = "Demo Location",
                    PriceInCents = 1499, // $14.99
                    IsActive = false,
                    StartTimeUtc = todayDateInTz.AddDays(3).AddHours(12).ToUniversalTime(),
                    EndTimeUtc = todayDateInTz.AddDays(3).AddHours(13).ToUniversalTime(),
                    RegistrationDeadlineUtc = todayDateInTz.AddDays(3).AddHours(11).ToUniversalTime(),
                    TimeZoneId = tzString,
                    CreatedBy = "System"
                },
                new Event
                {
                    Id = "12348765-aaaa-bbbb-cccc-0987654321ba",
                    TenantId = demoTenant.Id,
                    LocationId = demoTenant.Locations.First().Id,
                    Name = "Past Event",
                    Description = "An event that already occurred.",
                    EventType = Core.Constants.EventType.Workshop,
                    Capacity = 5,
                    IsActive = true,
                    StartTimeUtc = todayDateInTz.AddDays(-3).AddHours(12).ToUniversalTime(),
                    EndTimeUtc = todayDateInTz.AddDays(-3).AddHours(13).ToUniversalTime(),
                    TimeZoneId = tzString,
                    CreatedBy = "System"
                }
            };

            context.Tenants.Add(demoTenant);
            context.MembershipPlans.AddRange(demoPlans);
            context.Users.AddRange(demoUsers);
            context.Events.AddRange(demoEvents);
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
    /// Get basic configuration JSON string for basic (empty) tenant.
    /// </summary>
    private static string GetBasicConfig()
    {
        var basicConfig = new TenantConfig();

        return JsonSerializer.Serialize(
            basicConfig,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            }
        );
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
                LogoUrl = "https://ui-avatars.com/api/?name=Demo+Club&size=200&background=283845&color=fff"
            },
            Features = new FeatureFlags
            {
                EnableMemberships = true,
                EnableEventRegistration = true,
                EnablePayments = true
            },
            HomePage = new HomePageConfig
            {
                Hero = new HeroConfig
                {
                    Heading = "Welcome to the Demo VB Club",
                    Subheading = "Join us for fun and competitive volleyball events!",
                    ImageUrl = "https://images.unsplash.com/photo-1517649763962-0c623066013b?auto=format&fit=crop&w=800&q=80",
                    CtaText = "View Events",
                    CtaUrl = "/events"
                },
                About = new AboutConfig
                {
                    Heading = "About Demo VB Club",
                    Description = "Demo VB Club is dedicated to promoting volleyball in a fun and inclusive environment. Whether you're a beginner or a seasoned player, we have something for everyone.",
                    FeatureCards = new List<FeatureCardConfig>
                    {
                        new FeatureCardConfig
                        {
                            Title = "Competitive Play",
                            Description = "Join our competitive leagues and tournaments to test your skills.",
                            Icon = "balloon-fill",
                            DisplayOrder = 0
                        },
                        new FeatureCardConfig
                        {
                            Title = "Social Events",
                            Description = "Participate in fun social events and meet fellow volleyball enthusiasts.",
                            ImageUrl = "https://images.unsplash.com/photo-1504384308090-c894fdcc538d?auto=format&fit=crop&w=800&q=80",
                            Icon = "balloon-fill",
                            DisplayOrder = 1
                        },
                        new FeatureCardConfig
                        {
                            Title = "Training Programs",
                            Description = "Improve your skills with our expert-led training sessions.",
                            ImageUrl = "https://images.unsplash.com/photo-1521412644187-c49fa049e84d?auto=format&fit=crop&w=800&q=80",
                            Icon = "balloon-fill",
                            DisplayOrder = 2
                        }
                    }
                },
                Services = new ServicesConfig
                {
                    Heading = "Our Services",
                    Description = "Explore our full range of coaching, training, and sports experiences. From first serve to match point — we've got the right program for you.",
                    ServiceCards = new List<ServiceCardConfig>
                    {
                        new ServiceCardConfig
                        {
                            Title = "Private Coaching",
                            Description = "One-on-one coaching sessions tailored to your skill level.",
                            ImageUrl = "https://images.unsplash.com/photo-1517649763962-0c623066013b?auto=format&fit=crop&w=800&q=80",
                            Icon = "balloon-fill",
                            DisplayOrder = 0
                        },
                        new ServiceCardConfig
                        {
                            Title = "Group Clinics",
                            Subtitle = "Improve together!",
                            Description = "Join group clinics to learn and practice with others.",
                            ImageUrl = "https://images.unsplash.com/photo-1504384308090-c894fdcc538d?auto=format&fit=crop&w=800&q=80",
                            Icon = "balloon-fill",
                            LinkText = "Learn More",
                            LinkUrl = "/events",
                            DisplayOrder = 1
                        },
                        new ServiceCardConfig
                        {
                            Title = "Fitness Training",
                            Description = "This is a description.",
                            Icon = "balloon-fill",
                            DisplayOrder = 2
                        }
                    }
                },
                Visibility = new SectionVisibility
                {
                    ShowHero = true,
                    ShowAbout = true,
                    ShowServices = true
                }
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
    private static List<User> GenerateDemoUsers(string tenantId, string locationId, int count)
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
                LocationId = locationId,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                IsActive = random.Next(100) > 10 // 90% active
            });
        }

        return users;
    }
}
