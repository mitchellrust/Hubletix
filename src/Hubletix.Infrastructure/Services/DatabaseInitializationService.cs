using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Hubletix.Infrastructure.Persistence;
using Hubletix.Core.Entities;
using Hubletix.Core.Models;
using Hubletix.Core.Enums;
using System.Text.Json;
using Hubletix.Core.Constants;
using Finbuckle.MultiTenant.Abstractions;

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
    /// Initialize databases by applying migrations and seeding test tenants.
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
          var platformPlans = await SeedPlatformAsync(appContext);

          // Seed tenant store data with demo tenant if needed
          ClubTenantInfo? demoTenantInfo = await SeedTenantStoreAsync(
            tenantStoreContext,
            "demo"
          );

          if (demoTenantInfo != null)
          {
            // Seed application data with demo tenant info.
            await SeedAppDemoAsync(appContext, demoTenantInfo, platformPlans);
          }

          // Seed tenant store data with acme tenant if needed
          ClubTenantInfo? acmeTenantInfo = await SeedTenantStoreAsync(
            tenantStoreContext,
            "acme"
          );

          if (acmeTenantInfo != null)
          {
            // Seed application data with acme tenant info.
            await SeedAppAcmeAsync(appContext, acmeTenantInfo, platformPlans);
          }

          // Seed tenant store data with paused tenant if needed
          ClubTenantInfo? pausedTenantInfo = await SeedTenantStoreAsync(
            tenantStoreContext,
            "paused"
          );

          if (pausedTenantInfo != null)
          {
            // Seed application data with paused tenant info.
            await SeedAppPausedAsync(appContext, pausedTenantInfo, platformPlans);
          }

          // Seed cross-cutting data (payments, signup sessions)
          await SeedPaymentsAsync(appContext);
          await SeedSignupSessionsAsync(appContext, platformPlans);

          _logger.LogInformation("Database initialization completed successfully");
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "An error occurred while initializing the database");
          throw;
        }
    }

    private async Task<List<PlatformPlan>> SeedPlatformAsync(
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
            return platformPlans;
        }

        return await context.PlatformPlans.ToListAsync();
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
                _logger.LogInformation("Creating tenant '{Identifier}'...", tenantIdentifier);

                tenant = new ClubTenantInfo(
                    Id: Guid.NewGuid().ToString(),
                    Identifier: tenantIdentifier,  // This is the subdomain
                    Name: $"{tenantIdentifier.ToUpper()} VB Club"
                );

                context.TenantInfo.Add(tenant);
                await context.SaveChangesAsync();

                _logger.LogInformation("Tenant created with identifier: {Subdomain}", tenant.Identifier);
                _logger.LogInformation("Access the tenant at: http://{Subdomain}.localhost:5000", tenant.Identifier);
            }
            else
            {
                _logger.LogInformation("Tenant '{Identifier}' already exists", tenantIdentifier);
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
    /// Seed application database with demo tenant data.
    /// Demo tenant: Full featured with variety of data
    /// </summary>
    private async Task SeedAppDemoAsync(
      AppDbContext context,
      ClubTenantInfo demoTenantInfo,
      List<PlatformPlan> platformPlans
    )
    {
        try
        {
            var demoTenant = new Tenant
            {
                Id = demoTenantInfo.Id,
                TenantId = demoTenantInfo.Id,
                Name = demoTenantInfo.Name!,
                Subdomain = demoTenantInfo.Identifier,
                Status = TenantStatus.Active,
                StripeAccountId = "acct_demo123456789",
                ConfigJson = GetDemoConfig(),
                Locations = new List<Location>()
                {
                    new Location
                    {
                        Id = Guid.NewGuid().ToString(),
                        TenantId = demoTenantInfo.Id,
                        Name = "Default",
                        IsDefault = true,
                        IsActive = true
                    }
                }
            };

            var location = demoTenant.Locations.First();

            var demoPlans = new List<MembershipPlan>
            {
                new MembershipPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    TenantId = demoTenant.Id,
                    LocationId = location.Id,
                    Name = "Basic Membership",
                    Description = "Gym access during staffed hours.",
                    PriceInCents = 1000, // $10.00
                    BillingInterval = BillingIntervals.Monthly,
                    IsPriceDisplayedMonthly = true,
                    IsActive = true,
                    DisplayOrder = 0,
                    StripePriceId = "price_demo_basic_monthly",
                    CreatedBy = "System"
                },
                new MembershipPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    TenantId = demoTenant.Id,
                    LocationId = location.Id,
                    Name = "Premium Membership",
                    Description = "24/7 gym access.|Member-only events.",
                    PriceInCents = 12000, // $120.00, billed annually
                    BillingInterval = BillingIntervals.Annually,
                    IsPriceDisplayedMonthly = false,
                    IsActive = true,
                    DisplayOrder = 1,
                    StripePriceId = "price_demo_premium_annual",
                    CreatedBy = "System"
                },
                new MembershipPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    TenantId = demoTenant.Id,
                    LocationId = location.Id,
                    Name = "Ultra Membership",
                    Description = "24/7 gym access.|Member-only events.|Free guest passes.",
                    PriceInCents = 15000, // $150.00 billed annually, displayed monthly
                    BillingInterval = BillingIntervals.Annually,
                    IsPriceDisplayedMonthly = true,
                    IsActive = true,
                    DisplayOrder = 2,
                    StripePriceId = "price_demo_ultra_annual",
                    CreatedBy = "System"
                },
                new MembershipPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    TenantId = demoTenant.Id,
                    LocationId = location.Id,
                    Name = "Inactive Plan",
                    Description = "This plan is no longer available.",
                    PriceInCents = 500,
                    BillingInterval = BillingIntervals.Monthly,
                    IsPriceDisplayedMonthly = true,
                    IsActive = false,
                    DisplayOrder = 3,
                    CreatedBy = "System"
                }
            };

            // Save tenant, locations, and membership plans first
            context.Tenants.Add(demoTenant);
            context.MembershipPlans.AddRange(demoPlans);
            await context.SaveChangesAsync();

            // Create TenantSubscription for demo tenant
            var subscription = new TenantSubscription
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = demoTenant.Id,
                PlatformPlanId = platformPlans[1].Id, // Growth plan
                StripeCustomerId = "cus_demo123456",
                StripeSubscriptionId = "sub_demo123456",
                Status = SubscriptionStatus.Active,
                CurrentPeriodStart = DateTime.UtcNow.AddDays(-15),
                CurrentPeriodEnd = DateTime.UtcNow.AddDays(15),
                WillRenew = true
            };
            context.TenantSubscriptions.Add(subscription);
            await context.SaveChangesAsync();

            // Generate demo users
            var demoUsers = await GenerateDemoUsersAsync(
                context, 
                demoTenant.Id, 
                location.Id,
                demoPlans,
                8,
                "demo");

            // Get Timezone info for event dates
            var tzString = "America/Denver";
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(tzString);
            var todayInTz = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;

            // Find a coach for events
            var coach = demoUsers.FirstOrDefault(u => u.TenantUsers.Any(tu => tu.Role == TenantRole.Coach));

            var demoEvents = new List<Event>
            {
                new Event
                {
                    Id = Guid.NewGuid().ToString(),
                    TenantId = demoTenant.Id,
                    LocationId = location.Id,
                    Name = "Past Workshop",
                    Description = "A workshop that already happened.",
                    EventType = EventType.Workshop,
                    Capacity = 15,
                    IsActive = true,
                    PriceInCents = 1500,
                    StartTimeUtc = todayInTz.AddDays(-10).AddHours(18).ToUniversalTime(),
                    EndTimeUtc = todayInTz.AddDays(-10).AddHours(20).ToUniversalTime(),
                    TimeZoneId = tzString,
                    CoachId = coach?.TenantUsers.FirstOrDefault(tu => tu.TenantId == demoTenant.Id)?.Id,
                    CreatedBy = "System",
                    EventRegistrations = new List<EventRegistration>
                    {
                        new EventRegistration
                        {
                            Id = Guid.NewGuid().ToString(),
                            TenantId = demoTenant.Id,
                            PlatformUserId = demoUsers[0].Id,
                            Status = EventRegistrationStatus.Attended,
                            SignedUpAt = todayInTz.AddDays(-12).ToUniversalTime(),
                            CreatedBy = "System"
                        }
                    }
                },
                new Event
                {
                    Id = Guid.NewGuid().ToString(),
                    TenantId = demoTenant.Id,
                    LocationId = location.Id,
                    Name = "Today's Group Training",
                    Description = "Training session happening today!",
                    EventType = EventType.GroupTraining,
                    Capacity = 12,
                    IsActive = true,
                    PriceInCents = 0,
                    StartTimeUtc = todayInTz.AddHours(19).ToUniversalTime(),
                    EndTimeUtc = todayInTz.AddHours(21).ToUniversalTime(),
                    TimeZoneId = tzString,
                    CoachId = coach?.TenantUsers.FirstOrDefault(tu => tu.TenantId == demoTenant.Id)?.Id,
                    CreatedBy = "System"
                },
                new Event
                {
                    Id = "12345678-aaaa-bbbb-cccc-1234567890ab",
                    TenantId = demoTenant.Id,
                    LocationId = location.Id,
                    Name = "Multi-Day Tournament",
                    Description = "Join us for our annual volleyball tournament!",
                    EventType = EventType.Tournament,
                    PriceInCents = 2500,
                    LocationDetails = "Courts 1-4",
                    Capacity = 5,
                    RegistrationDeadlineUtc = todayInTz.AddDays(7).AddMinutes(-1).ToUniversalTime(),
                    IsActive = true,
                    StartTimeUtc = todayInTz.AddDays(8).AddHours(9).ToUniversalTime(),
                    EndTimeUtc = todayInTz.AddDays(9).AddHours(17).ToUniversalTime(),
                    TimeZoneId = tzString,
                    CreatedBy = "System",
                    EventRegistrations = new List<EventRegistration>
                    {
                        new EventRegistration
                        {
                            Id = Guid.NewGuid().ToString(),
                            TenantId = demoTenant.Id,
                            PlatformUserId = demoUsers[0].Id,
                            Status = EventRegistrationStatus.Registered,
                            SignedUpAt = todayInTz.AddDays(-1).ToUniversalTime(),
                            CreatedBy = "System"
                        },
                        new EventRegistration
                        {
                            Id = Guid.NewGuid().ToString(),
                            TenantId = demoTenant.Id,
                            PlatformUserId = demoUsers[2].Id,
                            Status = EventRegistrationStatus.Registered,
                            SignedUpAt = todayInTz.AddHours(-5).ToUniversalTime(),
                            CreatedBy = "System"
                        },
                        new EventRegistration
                        {
                            Id = Guid.NewGuid().ToString(),
                            TenantId = demoTenant.Id,
                            PlatformUserId = demoUsers[3].Id,
                            Status = EventRegistrationStatus.Cancelled,
                            CancellationReason = "Can't make it",
                            SignedUpAt = todayInTz.AddHours(-6).ToUniversalTime(),
                            CreatedBy = "System"
                        }
                    }
                },
                new Event
                {
                    Id = "87654321-bbbb-cccc-dddd-0987654321ba",
                    TenantId = demoTenant.Id,
                    LocationId = location.Id,
                    Name = "Event At Capacity (Waitlist)",
                    Description = "A popular social event with waitlist.",
                    EventType = EventType.Social,
                    LocationDetails = "Main Court",
                    Capacity = 2,
                    IsActive = true,
                    StartTimeUtc = todayInTz.AddDays(5).AddHours(18).ToUniversalTime(),
                    EndTimeUtc = todayInTz.AddDays(5).AddHours(20).ToUniversalTime(),
                    TimeZoneId = tzString,
                    CreatedBy = "System",
                    EventRegistrations = new List<EventRegistration>
                    {
                        new EventRegistration
                        {
                            Id = Guid.NewGuid().ToString(),
                            TenantId = demoTenant.Id,
                            PlatformUserId = demoUsers[0].Id,
                            Status = EventRegistrationStatus.Registered,
                            SignedUpAt = todayInTz.AddDays(-1).ToUniversalTime(),
                            CreatedBy = "System"
                        },
                        new EventRegistration
                        {
                            Id = Guid.NewGuid().ToString(),
                            TenantId = demoTenant.Id,
                            PlatformUserId = demoUsers[1].Id,
                            Status = EventRegistrationStatus.Registered,
                            SignedUpAt = todayInTz.AddDays(-1).AddHours(1).ToUniversalTime(),
                            CreatedBy = "System"
                        },
                        new EventRegistration
                        {
                            Id = Guid.NewGuid().ToString(),
                            TenantId = demoTenant.Id,
                            PlatformUserId = demoUsers[4].Id,
                            Status = EventRegistrationStatus.Waitlist,
                            SignedUpAt = todayInTz.AddDays(-1).AddHours(2).ToUniversalTime(),
                            CreatedBy = "System"
                        }
                    }
                },
                new Event
                {
                    Id = Guid.NewGuid().ToString(),
                    TenantId = demoTenant.Id,
                    LocationId = location.Id,
                    Name = "Inactive Event",
                    Description = "An event that is not published.",
                    EventType = EventType.Other,
                    Capacity = 10,
                    PriceInCents = 1000,
                    IsActive = false,
                    StartTimeUtc = todayInTz.AddDays(14).AddHours(10).ToUniversalTime(),
                    EndTimeUtc = todayInTz.AddDays(14).AddHours(12).ToUniversalTime(),
                    TimeZoneId = tzString,
                    CreatedBy = "System"
                },
                new Event
                {
                    Id = Guid.NewGuid().ToString(),
                    TenantId = demoTenant.Id,
                    LocationId = location.Id,
                    Name = "Future League Night",
                    Description = "Weekly competitive league play.",
                    EventType = EventType.League,
                    Capacity = 20,
                    IsActive = true,
                    PriceInCents = 0,
                    StartTimeUtc = todayInTz.AddDays(21).AddHours(19).ToUniversalTime(),
                    EndTimeUtc = todayInTz.AddDays(21).AddHours(22).ToUniversalTime(),
                    TimeZoneId = tzString,
                    CreatedBy = "System"
                }
            };

            context.Events.AddRange(demoEvents);
            await context.SaveChangesAsync();

            _logger.LogInformation("Demo tenant seeded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding demo tenant");
            throw;
        }
    }

    /// <summary>
    /// Seed application database with acme tenant data.
    /// Acme tenant: Active with basic data
    /// </summary>
    private async Task SeedAppAcmeAsync(
      AppDbContext context,
      ClubTenantInfo acmeTenantInfo,
      List<PlatformPlan> platformPlans
    )
    {
        try
        {
            var acmeTenant = new Tenant
            {
                Id = acmeTenantInfo.Id,
                TenantId = acmeTenantInfo.Id,
                Name = acmeTenantInfo.Name!,
                Subdomain = acmeTenantInfo.Identifier,
                Status = TenantStatus.Active,
                StripeAccountId = "acct_acme987654321",
                ConfigJson = GetBasicConfig(),
                Locations = new List<Location>()
                {
                    new Location
                    {
                        Id = Guid.NewGuid().ToString(),
                        TenantId = acmeTenantInfo.Id,
                        Name = "Default",
                        IsDefault = true,
                        IsActive = true
                    }
                }
            };

            var location = acmeTenant.Locations.First();

            var acmePlans = new List<MembershipPlan>
            {
                new MembershipPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    TenantId = acmeTenant.Id,
                    LocationId = location.Id,
                    Name = "Monthly Pass",
                    Description = "Standard monthly access.",
                    PriceInCents = 5000,
                    BillingInterval = BillingIntervals.Monthly,
                    IsPriceDisplayedMonthly = true,
                    IsActive = true,
                    DisplayOrder = 0,
                    StripePriceId = "price_acme_monthly",
                    CreatedBy = "System"
                },
                new MembershipPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    TenantId = acmeTenant.Id,
                    LocationId = location.Id,
                    Name = "Drop-In",
                    Description = "Single visit pass.",
                    PriceInCents = 1500,
                    BillingInterval = BillingIntervals.OneTime,
                    IsPriceDisplayedMonthly = false,
                    IsActive = true,
                    DisplayOrder = 1,
                    StripePriceId = "price_acme_dropin",
                    CreatedBy = "System"
                }
            };

            context.Tenants.Add(acmeTenant);
            context.MembershipPlans.AddRange(acmePlans);
            await context.SaveChangesAsync();

            // Create TenantSubscription for acme tenant (trialing)
            var subscription = new TenantSubscription
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = acmeTenant.Id,
                PlatformPlanId = platformPlans[0].Id, // Starter plan
                StripeCustomerId = "cus_acme654321",
                StripeSubscriptionId = "sub_acme654321",
                Status = SubscriptionStatus.Trialing,
                CurrentPeriodStart = DateTime.UtcNow.AddDays(-5),
                CurrentPeriodEnd = DateTime.UtcNow.AddDays(9), // 14-day trial
                WillRenew = true
            };
            context.TenantSubscriptions.Add(subscription);
            await context.SaveChangesAsync();

            // Generate users for acme tenant
            var acmeUsers = await GenerateDemoUsersAsync(
                context,
                acmeTenant.Id,
                location.Id,
                acmePlans,
                5,
                "acme");

            // Create a few simple events
            var tzString = "America/Chicago";
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(tzString);
            var todayInTz = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;

            var acmeEvents = new List<Event>
            {
                new Event
                {
                    Id = Guid.NewGuid().ToString(),
                    TenantId = acmeTenant.Id,
                    LocationId = location.Id,
                    Name = "Weekly Pickup Game",
                    EventType = EventType.Social,
                    Capacity = 16,
                    IsActive = true,
                    PriceInCents = 0,
                    StartTimeUtc = todayInTz.AddDays(3).AddHours(18).ToUniversalTime(),
                    EndTimeUtc = todayInTz.AddDays(3).AddHours(20).ToUniversalTime(),
                    TimeZoneId = tzString,
                    CreatedBy = "System"
                },
                new Event
                {
                    Id = Guid.NewGuid().ToString(),
                    TenantId = acmeTenant.Id,
                    LocationId = location.Id,
                    Name = "Skills Clinic",
                    Description = "Improve your fundamentals.",
                    EventType = EventType.GroupTraining,
                    Capacity = 10,
                    IsActive = true,
                    PriceInCents = 2000,
                    StartTimeUtc = todayInTz.AddDays(10).AddHours(14).ToUniversalTime(),
                    EndTimeUtc = todayInTz.AddDays(10).AddHours(16).ToUniversalTime(),
                    TimeZoneId = tzString,
                    CreatedBy = "System",
                    EventRegistrations = new List<EventRegistration>
                    {
                        new EventRegistration
                        {
                            Id = Guid.NewGuid().ToString(),
                            TenantId = acmeTenant.Id,
                            PlatformUserId = acmeUsers[0].Id,
                            Status = EventRegistrationStatus.Registered,
                            SignedUpAt = todayInTz.AddDays(-1).ToUniversalTime(),
                            CreatedBy = "System"
                        }
                    }
                }
            };

            context.Events.AddRange(acmeEvents);
            await context.SaveChangesAsync();

            _logger.LogInformation("Acme tenant seeded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding acme tenant");
            throw;
        }
    }

    /// <summary>
    /// Seed application database with paused tenant data.
    /// Paused tenant: Suspended status with past_due subscription
    /// </summary>
    private async Task SeedAppPausedAsync(
      AppDbContext context,
      ClubTenantInfo pausedTenantInfo,
      List<PlatformPlan> platformPlans
    )
    {
        try
        {
            var pausedTenant = new Tenant
            {
                Id = pausedTenantInfo.Id,
                TenantId = pausedTenantInfo.Id,
                Name = pausedTenantInfo.Name!,
                Subdomain = pausedTenantInfo.Identifier,
                Status = TenantStatus.Suspended,
                StripeAccountId = "acct_paused111222333",
                ConfigJson = GetBasicConfig(),
                Locations = new List<Location>()
                {
                    new Location
                    {
                        Id = Guid.NewGuid().ToString(),
                        TenantId = pausedTenantInfo.Id,
                        Name = "Default",
                        IsDefault = true,
                        IsActive = true
                    }
                }
            };

            context.Tenants.Add(pausedTenant);
            await context.SaveChangesAsync();

            // Create TenantSubscription for paused tenant (past_due)
            var subscription = new TenantSubscription
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = pausedTenant.Id,
                PlatformPlanId = platformPlans[1].Id, // Growth plan
                StripeCustomerId = "cus_paused111222",
                StripeSubscriptionId = "sub_paused111222",
                Status = SubscriptionStatus.PastDue,
                CurrentPeriodStart = DateTime.UtcNow.AddDays(-35),
                CurrentPeriodEnd = DateTime.UtcNow.AddDays(-5), // Expired 5 days ago
                WillRenew = false
            };
            context.TenantSubscriptions.Add(subscription);
            await context.SaveChangesAsync();

            // Create minimal users for paused tenant
            var location = pausedTenant.Locations.First();
            await GenerateDemoUsersAsync(
                context,
                pausedTenant.Id,
                location.Id,
                new List<MembershipPlan>(),
                2,
                "paused");

            _logger.LogInformation("Paused tenant seeded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding paused tenant");
            throw;
        }
    }

    /// <summary>
    /// Seed payment records across tenants.
    /// </summary>
    private async Task SeedPaymentsAsync(AppDbContext context)
    {
        var paymentsExist = await context.Payments.AnyAsync();
        if (paymentsExist)
        {
            return;
        }

        _logger.LogInformation("Seeding payment records...");

        var demoTenant = await context.Tenants.FirstOrDefaultAsync(t => t.Subdomain == "demo");
        var acmeTenant = await context.Tenants.FirstOrDefaultAsync(t => t.Subdomain == "acme");
        var demoUsers = await context.PlatformUsers
            .Include(pu => pu.TenantUsers)
            .Where(pu => pu.TenantUsers.Any(tu => tu.TenantId == demoTenant!.Id))
            .ToListAsync();

        if (demoTenant == null || acmeTenant == null || !demoUsers.Any())
        {
            return;
        }

        var payments = new List<Payment>
        {
            new Payment
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = demoTenant.Id,
                PlatformUserId = demoUsers[0].Id,
                StripePaymentId = "pi_demo_event_001",
                AmountInCents = 2500,
                Status = "succeeded",
                PaymentType = "one_time",
                Description = "Event Registration Fee",
                CreatedAt = DateTime.UtcNow.AddDays(-8)
            },
            new Payment
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = demoTenant.Id,
                PlatformUserId = demoUsers[1].Id,
                AmountInCents = 1000,
                Status = "succeeded",
                PaymentType = "subscription",
                StripePaymentId = "pi_demo_membership_001",
                CreatedAt = DateTime.UtcNow.AddDays(-15)
            },
            new Payment
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = demoTenant.Id,
                PlatformUserId = demoUsers[2].Id,
                AmountInCents = 1500,
                Status = "failed",
                PaymentType = "one_time",
                StripePaymentId = "pi_demo_failed_001",
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new Payment
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = demoTenant.Id,
                PlatformUserId = demoUsers[0].Id,
                AmountInCents = 2500,
                Status = "refunded",
                PaymentType = "refund",
                StripePaymentId = "pi_demo_refund_001",
                CreatedAt = DateTime.UtcNow.AddDays(-20),
            },
            new Payment
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = acmeTenant.Id,
                PlatformUserId = demoUsers[0].Id, // Cross-tenant payment
                AmountInCents = 2000,
                Currency = "usd",
                Status = "succeeded",
                PaymentType = "one_time",
                StripePaymentId = "pi_acme_event_001",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new Payment
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = demoTenant.Id,
                PlatformUserId = demoUsers[3].Id,
                AmountInCents = 12000,
                Currency = "usd",
                Status = "pending",
                PaymentType = "subscription",
                StripePaymentId = "pi_demo_pending_001",
                CreatedAt = DateTime.UtcNow.AddHours(-2)
            }
        };

        context.Payments.AddRange(payments);
        await context.SaveChangesAsync();

        _logger.LogInformation("Payment records seeded successfully");
    }

    /// <summary>
    /// Seed signup session records in various states.
    /// </summary>
    private async Task SeedSignupSessionsAsync(AppDbContext context, List<PlatformPlan> platformPlans)
    {
        var sessionsExist = await context.SignupSessions.AnyAsync();
        if (sessionsExist)
        {
            return;
        }

        _logger.LogInformation("Seeding signup session records...");

        var completedTenant = await context.Tenants.FirstOrDefaultAsync(t => t.Subdomain == "demo");
        var completedUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "mary.johnson@demo.com");

        var sessions = new List<SignupSession>
        {
            new SignupSession
            {
                Id = Guid.NewGuid().ToString(),
                PlatformPlanId = platformPlans[0].Id,
                Email = "completed@test.com",
                FirstName = "Completed",
                LastName = "User",
                OrganizationName = "Test Org",
                Subdomain = "testcompleted",
                UserId = completedUser?.Id,
                TenantId = completedTenant?.Id,
                State = SignupSessionState.Completed,
                StripeCheckoutSessionId = "cs_completed_123",
                CompletedAt = DateTime.UtcNow.AddDays(-7),
                ExpiresAt = DateTime.UtcNow.AddDays(-6).AddHours(17),
                LastActivityAt = DateTime.UtcNow.AddDays(-7),
                CreatedAt = DateTime.UtcNow.AddDays(-7).AddHours(-1)
            },
            new SignupSession
            {
                Id = Guid.NewGuid().ToString(),
                PlatformPlanId = platformPlans[1].Id,
                Email = "pending@test.com",
                FirstName = "Pending",
                LastName = "Payment",
                OrganizationName = "Pending Org",
                Subdomain = "pendingtest",
                State = SignupSessionState.BillingComplete,
                StripeCheckoutSessionId = "cs_pending_456",
                ExpiresAt = DateTime.UtcNow.AddHours(12),
                LastActivityAt = DateTime.UtcNow.AddHours(-2),
                CreatedAt = DateTime.UtcNow.AddHours(-3)
            },
            new SignupSession
            {
                Id = Guid.NewGuid().ToString(),
                PlatformPlanId = platformPlans[2].Id,
                Email = "abandoned@test.com",
                State = SignupSessionState.Started,
                ExpiresAt = DateTime.UtcNow.AddHours(-5), // Expired
                LastActivityAt = DateTime.UtcNow.AddDays(-2),
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new SignupSession
            {
                Id = Guid.NewGuid().ToString(),
                PlatformPlanId = platformPlans[0].Id,
                Email = "failed@test.com",
                FirstName = "Failed",
                LastName = "Signup",
                State = SignupSessionState.BillingStarted,
                ErrorMessage = "Payment processing failed",
                StripeCheckoutSessionId = "cs_failed_789",
                ExpiresAt = DateTime.UtcNow.AddHours(6),
                LastActivityAt = DateTime.UtcNow.AddHours(-1),
                CreatedAt = DateTime.UtcNow.AddHours(-2)
            }
        };

        context.SignupSessions.AddRange(sessions);
        await context.SaveChangesAsync();

        _logger.LogInformation("Signup session records seeded successfully");
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
    /// and TenantUser memberships. Includes variety of roles and statuses.
    /// </summary>
    private async Task<List<PlatformUser>> GenerateDemoUsersAsync(
        AppDbContext context,
        string tenantId,
        string locationId,
        List<MembershipPlan> membershipPlans,
        int count,
        string tenantPrefix)
    {
        var firstNames = new[] 
        { 
            "James", "Mary", "John", "Patricia", "Robert", "Jennifer", "Michael", "Linda", 
            "Sarah", "David", "Jessica", "William", "Emily", "Richard", "Daniel"
        };
        
        var lastNames = new[] 
        { 
            "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis",
            "Rodriguez", "Martinez", "Wilson", "Anderson", "Taylor", "Moore", "Jackson"
        };

        var random = new Random(tenantPrefix.GetHashCode()); // Consistent per tenant
        var platformUsers = new List<PlatformUser>();

        // First user is always owner/admin
        var isFirstUser = true;

        for (int i = 0; i < count; i++)
        {
            var firstName = firstNames[i % firstNames.Length];
            var lastName = lastNames[i % lastNames.Length];
            var email = $"{firstName.ToLower()}.{lastName.ToLower()}@{tenantPrefix}.com";
            
            // Check if user already exists (for cross-tenant scenarios)
            var existingIdentityUser = await _userManager.FindByEmailAsync(email);
            PlatformUser? platformUser = null;

            if (existingIdentityUser != null)
            {
                // User exists from another tenant, get their PlatformUser
                platformUser = await context.PlatformUsers
                    .Include(pu => pu.TenantUsers)
                    .FirstOrDefaultAsync(pu => pu.IdentityUserId == existingIdentityUser.Id);
            }
            else
            {
                // Create new Identity user
                var identityUser = new User
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(identityUser, "Test123!");
                
                if (!result.Succeeded)
                {
                    _logger.LogWarning("Failed to create user {Email}", email);
                    continue;
                }

                // Add platform admin role to first user of demo tenant
                if (tenantPrefix == "demo" && i == 0)
                {
                    await _userManager.AddToRoleAsync(identityUser, PlatformRoles.PlatformAdmin);
                }

                // Create PlatformUser
                platformUser = new PlatformUser
                {
                    IdentityUserId = identityUser.Id,
                    FirstName = firstName,
                    LastName = lastName,
                    IsActive = true,
                };

                context.PlatformUsers.Add(platformUser);
                await context.SaveChangesAsync();
            }

            if (platformUser == null)
            {
                continue;
            }

            // Determine role and status
            TenantRole role;
            TenantUserStatus status;
            string? membershipPlanId = null;

            if (isFirstUser)
            {
                // First user is owner/admin
                role = TenantRole.Admin;
                status = TenantUserStatus.Active;
                membershipPlanId = membershipPlans.Any() ? membershipPlans[0].Id : null;
                isFirstUser = false;
            }
            else
            {
                // Vary roles and statuses
                var roleChoice = random.Next(100);
                if (roleChoice < 60)
                {
                    role = TenantRole.Member;
                }
                else if (roleChoice < 85)
                {
                    role = TenantRole.Coach;
                }
                else
                {
                    role = TenantRole.Admin;
                }

                var statusChoice = random.Next(100);
                if (statusChoice < 75)
                {
                    status = TenantUserStatus.Active;
                    membershipPlanId = membershipPlans.Any() ? membershipPlans[random.Next(membershipPlans.Count)].Id : null;
                }
                else if (statusChoice < 90)
                {
                    status = TenantUserStatus.Inactive;
                }
                else
                {
                    status = TenantUserStatus.PendingInvite;
                }
            }

            // Create TenantUser membership
            var tenantUser = new TenantUser
            {
                TenantId = tenantId,
                PlatformUserId = platformUser.Id,
                Role = role,
                Status = status,
                IsOwner = platformUsers.Count == 0, // First user is owner
                LocationId = locationId,
                MembershipPlanId = membershipPlanId,
                CreatedBy = "System"
            };

            context.TenantUsers.Add(tenantUser);
            await context.SaveChangesAsync();

            platformUsers.Add(platformUser);
        }

        // Create one cross-tenant user if this is acme tenant
        if (tenantPrefix == "acme" && platformUsers.Count > 0)
        {
            // Get James Smith from demo tenant and add them to acme
            var demoTenant = await context.Tenants.FirstOrDefaultAsync(t => t.Subdomain == "demo");
            if (demoTenant != null)
            {
                var crossTenantUser = await context.PlatformUsers
                    .Include(pu => pu.TenantUsers)
                    .FirstOrDefaultAsync(pu => pu.TenantUsers.Any(tu => tu.TenantId == demoTenant.Id) 
                                             && pu.FirstName == "James");

                if (crossTenantUser != null && !crossTenantUser.TenantUsers.Any(tu => tu.TenantId == tenantId))
                {
                    var crossTenantMembership = new TenantUser
                    {
                        TenantId = tenantId,
                        PlatformUserId = crossTenantUser.Id,
                        Role = TenantRole.Member, // Different role in this tenant
                        Status = TenantUserStatus.Active,
                        IsOwner = false,
                        LocationId = locationId,
                        MembershipPlanId = membershipPlans.Any() ? membershipPlans[0].Id : null,
                        CreatedBy = "System"
                    };

                    context.TenantUsers.Add(crossTenantMembership);
                    await context.SaveChangesAsync();

                    _logger.LogInformation("Created cross-tenant membership for user {Name}", 
                        $"{crossTenantUser.FirstName} {crossTenantUser.LastName}");
                }
            }
        }

        _logger.LogInformation("Generated {Count} users for tenant '{Tenant}'", platformUsers.Count, tenantPrefix);
        return platformUsers;
    }
}
