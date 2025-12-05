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
    /// Initialize databases by applying migrations and seeding admin tenant if needed.
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
          ClubTenantInfo? adminTenantInfo = await SeedTenantStoreAsync(tenantStoreContext);

          if (adminTenantInfo != null)
          {
            // Seed application data with admin tenant info.
            await SeedAppAsync(appContext, adminTenantInfo);
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
    /// Seed tenant store with admin tenant data if needed.
    /// </summary>
    private async Task<ClubTenantInfo?> SeedTenantStoreAsync(TenantStoreDbContext context)
    {
        ClubTenantInfo? adminTenant = null;
        try
        {
            // Check if any tenants exist
            var tenantExists = await context.TenantInfo.AnyAsync();
            
            if (!tenantExists)
            {
                _logger.LogInformation("No tenants found. Creating admin tenant...");

                adminTenant = new ClubTenantInfo(
                    Id: Guid.NewGuid().ToString(),
                    Identifier: "admin",  // This is the subdomain
                    Name: "Admin Tenant"
                );

                context.TenantInfo.Add(adminTenant);
                await context.SaveChangesAsync();

                _logger.LogInformation("Admin tenant created with identifier: {Subdomain}", adminTenant.Identifier);
                _logger.LogInformation("Access the admin tenant at: https://{Subdomain}.localhost:5001", adminTenant.Identifier);
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

        return adminTenant;
    }

    /// <summary>
    /// Seed application database with admin tenant data if needed.
    /// This runs in a tenant-agnostic context.
    /// </summary>
    private async Task SeedAppAsync(
      AppDbContext context,
      ClubTenantInfo adminTenantInfo
    )
    {
        try
        {
            // Add any global seed data here (lookup tables, reference data, etc.)
            var adminTenant = new Tenant
            {
                Id = adminTenantInfo.Id,
                TenantId = adminTenantInfo.Id,
                Name = adminTenantInfo.Name!,
                Subdomain = adminTenantInfo.Identifier,
                IsActive = true,
                ConfigJson = GetAdminConfig()
            };

            context.Tenants.Add(adminTenant);
            await context.SaveChangesAsync();

            _logger.LogInformation("Admin tenant created with identifier: {Subdomain}", adminTenant.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding app database");
            throw;
        }
    }

    /// <summary>
    /// Get default configuration JSON for admin tenant.
    /// </summary>
    private static string GetAdminConfig()
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
