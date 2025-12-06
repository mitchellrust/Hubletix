using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ClubManagement.Infrastructure.Persistence;
using ClubManagement.Core.Entities;

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

            context.Tenants.Add(demoTenant);
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
    /// Get default configuration JSON for demo tenant.
    /// </summary>
    private static string GetDemoConfig()
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
            "enableEventRegistrations": true,
            "enablePayments": true
          },
          "settings": {
            "timezone": "America/Boise",
            "defaultCurrency": "usd"
          }
        }
        """;
    }
}
