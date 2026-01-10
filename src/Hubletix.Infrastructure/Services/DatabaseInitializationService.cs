using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Hubletix.Infrastructure.Persistence;
using Hubletix.Core.Entities;
using Hubletix.Core.Models;
using Hubletix.Core.Enums;
using System.Text.Json;
using Hubletix.Core.Constants;

namespace Hubletix.Infrastructure.Services;

/// <summary>
/// Service responsible for database initialization, migration application, and seeding.
/// Called on application startup to ensure database schema is up-to-date.
/// </summary>
public class DatabaseInitializationService
{
    private readonly ILogger<DatabaseInitializationService> _logger;
    private readonly UserManager<User> _userManager;

    public DatabaseInitializationService(
        ILogger<DatabaseInitializationService> logger,
        UserManager<User> userManager)
    {
        _logger = logger;
        _userManager = userManager;
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

          // Seed platform wide data, i.e. platform plans
          await SeedPlatformAsync(appContext);

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

    private async Task SeedPlatformAsync(
        AppDbContext context
    )
    {
        var plansExist = await context.PlatformPlans.AnyAsync();
        if (!plansExist)
        {
            _logger.LogInformation("Seeding platform plans...");

            var platformPlans = new List<PlatformPlan>
            {
                new PlatformPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Starter",
                    Description = "Basic features for small clubs.",
                    PriceInCents = 2900, // $ 29.00,
                    StripeProductId = "prod_TlQitMVQTItdGh",
                    StripePriceId = "price_1SntqmIRfoYJ66ha4mdc7GMo",
                },
                new PlatformPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Growth",
                    Description = "Advanced features for growing clubs.",
                    PriceInCents = 11900, // $119.00,
                    StripeProductId = "prod_TlQikV4h2hysiY",
                    StripePriceId = "price_1SntqyIRfoYJ66hacitOlh9O",
                },
                new PlatformPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Professional",
                    Description = "All features for large clubs.",
                    PriceInCents = 39900, // $399.00
                    StripeProductId = "prod_TlQi4bCt3pG6wK",
                    StripePriceId = "price_1Sntr8IRfoYJ66haF8r2v88s",
                }
            };

            context.PlatformPlans.AddRange(platformPlans);
            await context.SaveChangesAsync();

            _logger.LogInformation("Platform plans seeded successfully");
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
                Status = TenantStatus.Active,
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
                Status = TenantStatus.Active,
                ConfigJson = GetDemoConfig(),
                Locations = new List<Location>()
                {
                    new Location
                    {
                        Id = Guid.NewGuid().ToString(),
                        TenantId = demoTenantInfo.Id,
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
                    BillingInterval = BillingIntervals.Monthly,
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
                    BillingInterval = BillingIntervals.Annually,
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
                    BillingInterval = BillingIntervals.Annually,
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
                    BillingInterval = BillingIntervals.OneTime,
                    IsPriceDisplayedMonthly = false,
                    IsActive = true,
                    DisplayOrder = 3,
                    CreatedBy = "System"
                }
            };

            // Save tenant, locations, and membership plans first so they exist for FK constraints
            context.Tenants.Add(demoTenant);
            context.MembershipPlans.AddRange(demoPlans);
            await context.SaveChangesAsync();

            // Generate demo users (creates Identity users, PlatformUsers, and TenantUsers)
            var demoPlatformUsers = await GenerateDemoUsersAsync(
                context, 
                demoTenant.Id, 
                demoTenant.Locations.First().Id,
                demoPlans,
                10);

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
                    EventType = EventType.Other,
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
                    EventType = EventType.Tournament,
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
                            TenantId = demoTenantInfo.Id,
                            EventId = "12345678-aaaa-bbbb-cccc-1234567890ab",
                            PlatformUserId = demoPlatformUsers[0].Id,
                            Status = EventRegistrationStatus.Registered,
                            SignedUpAt = todayDateInTz.AddDays(-1).AddHours(-2).ToUniversalTime(),
                            CreatedBy = "System"
                        },
                        new EventRegistration
                        {
                            Id = Guid.NewGuid().ToString(),
                            TenantId = demoTenantInfo.Id,
                            EventId = "12345678-aaaa-bbbb-cccc-1234567890ab",
                            PlatformUserId = demoPlatformUsers[2].Id,
                            Status = EventRegistrationStatus.Registered,
                            SignedUpAt = todayDateInTz.AddHours(-5).ToUniversalTime(),
                            CreatedBy = "System"
                        },
                        new EventRegistration
                        {
                            Id = Guid.NewGuid().ToString(),
                            TenantId = demoTenantInfo.Id,
                            EventId = "12345678-aaaa-bbbb-cccc-1234567890ab",
                            PlatformUserId = demoPlatformUsers[3].Id,
                            Status = EventRegistrationStatus.Cancelled,
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
                    EventType = EventType.Social,
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
                            TenantId = demoTenantInfo.Id,
                            EventId = "87654321-bbbb-cccc-dddd-0987654321ba",
                            PlatformUserId = demoPlatformUsers[0].Id,
                            Status = EventRegistrationStatus.Registered,
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
                    EventType = EventType.Social,
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
                    EventType = EventType.Workshop,
                    Capacity = 5,
                    IsActive = true,
                    StartTimeUtc = todayDateInTz.AddDays(-3).AddHours(12).ToUniversalTime(),
                    EndTimeUtc = todayDateInTz.AddDays(-3).AddHours(13).ToUniversalTime(),
                    TimeZoneId = tzString,
                    CreatedBy = "System"
                }
            };

            // Save events (tenant, locations, plans, and users already saved earlier)
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
                DefaultCurrency = "usd",
                DefaultCountry = "US",
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
    /// Generate demo users with proper Identity authentication, PlatformUser domain entities,
    /// and TenantUser memberships.
    /// </summary>
    private async Task<List<PlatformUser>> GenerateDemoUsersAsync(
        AppDbContext context,
        string tenantId,
        string locationId,
        List<MembershipPlan> membershipPlans,
        int count)
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
        var platformUsers = new List<PlatformUser>();
        var roles = new[] { TenantRole.Member, TenantRole.Coach, TenantRole.Admin };

        for (int i = 0; i < count; i++)
        {
            var firstName = firstNames[i % firstNames.Length];
            var lastName = lastNames[i % lastNames.Length];
            var email = $"{firstName.ToLower()}.{lastName.ToLower()}{(i > 50 ? i.ToString() : "")}@demo.com";
            var isActive = random.Next(100) > 10; // 90% active
            
            // Create Identity user with password
            var identityUser = new User
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(identityUser, "Demo123!"); // Demo password
            
            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed to create demo user {Email}: {Errors}", 
                    email, string.Join(", ", result.Errors.Select(e => e.Description)));
                continue;
            }

            // Create PlatformUser (domain entity)
            var platformUser = new PlatformUser
            {
                IdentityUserId = identityUser.Id,
                FirstName = firstName,
                LastName = lastName,
                IsActive = isActive,
                DefaultTenantId = tenantId
            };

            context.PlatformUsers.Add(platformUser);
            await context.SaveChangesAsync(); // Save to get PlatformUser.Id

            // Create TenantUser membership with random role
            var role = roles[random.Next(roles.Length)];
            var tenantUser = new TenantUser
            {
                TenantId = tenantId,
                PlatformUserId = platformUser.Id,
                Role = role,
                Status = isActive ? TenantUserStatus.Active : TenantUserStatus.Inactive,
                IsOwner = false,
                LocationId = locationId,
                MembershipPlanId = isActive ? membershipPlans[random.Next(membershipPlans.Count)].Id : null,
                CreatedBy = "System"
            };

            context.TenantUsers.Add(tenantUser);
            await context.SaveChangesAsync();

            platformUsers.Add(platformUser);
        }

        _logger.LogInformation("Generated {Count} demo users with PlatformUser and TenantUser entities", platformUsers.Count);
        return platformUsers;
    }
}
