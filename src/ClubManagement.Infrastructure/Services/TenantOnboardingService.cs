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

    /// <summary>
    /// Sets up Stripe Connect for an existing tenant.
    /// Creates a Stripe Connect account and generates an onboarding link.
    /// </summary>
    /// <param name="tenantId">The tenant ID</param>
    /// <param name="adminEmail">Admin email for the Connect account</param>
    /// <param name="refreshUrl">URL to redirect if the onboarding link expires</param>
    /// <param name="returnUrl">URL to redirect after onboarding completion</param>
    /// <returns>The Stripe onboarding URL</returns>
    Task<string> SetupStripeConnectAsync(string tenantId, string adminEmail, string refreshUrl, string returnUrl);
}

public class TenantOnboardingService : ITenantOnboardingService
{
    private readonly AppDbContext _dbContext;
    private readonly IStripeConnectService _stripeConnectService;
    private readonly ITenantConfigService _tenantConfigService;

    public TenantOnboardingService(
        AppDbContext dbContext,
        IMultiTenantContextAccessor multiTenantContextAccessor,
        IStripeConnectService stripeConnectService,
        ITenantConfigService tenantConfigService)
    {
        _dbContext = dbContext;
        _stripeConnectService = stripeConnectService;
        _tenantConfigService = tenantConfigService;
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

    public async Task<string> SetupStripeConnectAsync(
        string tenantId,
        string adminEmail,
        string refreshUrl,
        string returnUrl)
    {
        // Get the tenant from the database
        var tenant = await _tenantConfigService.GetTenantAsync(tenantId);
        if (tenant == null)
        {
            throw new InvalidOperationException($"Tenant with ID '{tenantId}' not found.");
        }

        // Check if already has a Stripe account
        if (!string.IsNullOrEmpty(tenant.StripeAccountId))
        {
            throw new InvalidOperationException("Tenant already has a Stripe Connect account.");
        }

        // Parse tenant configuration
        var tenantConfig = ParseTenantConfig(tenant.ConfigJson);

        // Create Stripe Connect account
        var accountId = await _stripeConnectService.CreateConnectAccountAsync(
            tenant.Id,
            tenant.Name,
            adminEmail,
            tenantConfig
        );

        // Save the account ID to the tenant
        tenant.StripeAccountId = accountId;
        await _dbContext.SaveChangesAsync();

        // Create and return onboarding link
        var onboardingUrl = await _stripeConnectService.CreateAccountLinkAsync(
            accountId,
            refreshUrl,
            returnUrl
        );

        return onboardingUrl;
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

    /// <summary>
    /// Parse tenant configuration JSON into TenantConfig object.
    /// Returns default config if JSON is null or invalid.
    /// </summary>
    private static TenantConfig ParseTenantConfig(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return new TenantConfig();
        }

        try
        {
            return JsonSerializer.Deserialize<TenantConfig>(
                configJson,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
            ) ?? new TenantConfig();
        }
        catch
        {
            return new TenantConfig();
        }
    }}