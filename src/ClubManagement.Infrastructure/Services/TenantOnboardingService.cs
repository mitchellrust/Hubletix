using Microsoft.EntityFrameworkCore;
using ClubManagement.Core.Entities;
using ClubManagement.Core.Constants;
using ClubManagement.Infrastructure.Persistence;
using ClubManagement.Core.Models;
using System.Text.Json;

namespace ClubManagement.Infrastructure.Services;

/// <summary>
/// Service for tenant onboarding flow.
/// Manages the complete signup process from plan selection to activation.
/// </summary>
public interface ITenantOnboardingService
{
    /// <summary>
    /// Step 1: Create a new signup session when a plan is selected
    /// </summary>
    Task<SignupSession> StartSignupSessionAsync(
        string platformPlanId,
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Step 2: Create admin user and associate with signup session
    /// </summary>
    Task<User> CreateAdminUserAsync(
        string signupSessionId,
        string email,
        string firstName,
        string lastName,
        string password, // TODO: Will be hashed when Identity is implemented
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Step 3: Create tenant in PendingActivation status
    /// </summary>
    Task<Tenant> CreateTenantAsync(
        string signupSessionId,
        string organizationName,
        string subdomain,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Step 4: Initialize Stripe billing and get checkout URL
    /// </summary>
    Task<string> InitializeBillingAsync(
        string signupSessionId,
        string successUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Step 5: Activate tenant after successful payment (called by webhook)
    /// </summary>
    Task ActivateTenantAsync(
        string stripeSubscriptionId,
        string stripeCustomerId,
        DateTime currentPeriodStart,
        DateTime currentPeriodEnd,
        string? stripeCheckoutSessionId = null,
        string? signupSessionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handle billing failure (called by webhook)
    /// </summary>
    Task HandleBillingFailureAsync(
        string stripeCheckoutSessionId,
        string errorMessage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get signup session by ID (for resuming abandoned sessions)
    /// </summary>
    Task<SignupSession?> GetSignupSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resume an abandoned signup session
    /// </summary>
    Task<SignupSession> ResumeSignupSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets up Stripe Connect for an existing tenant (existing functionality)
    /// </summary>
    Task<string> SetupStripeConnectAsync(
        string tenantId,
        string adminEmail,
        string refreshUrl,
        string returnUrl,
        CancellationToken cancellationToken = default);
}

public class TenantOnboardingService : ITenantOnboardingService
{
    private readonly AppDbContext _dbContext;
    private readonly TenantStoreDbContext _tenantStoreDbContext;
    private readonly IStripeConnectService _stripeConnectService;
    private readonly IStripePlatformService _stripePlatformService;
    private readonly ITenantConfigService _tenantConfigService;
    private const string SIGNUP_SESSION_ID_KEY = "signup_session_id";
    private const string TENANT_ID_KEY = "tenant_id";
    private const string PLATFORM_PLAN_ID_KEY = "plan_id";

    public TenantOnboardingService(
        AppDbContext dbContext,
        TenantStoreDbContext tenantStoreDbContext,
        IStripeConnectService stripeConnectService,
        IStripePlatformService stripePlatformService,
        ITenantConfigService tenantConfigService)
    {
        _dbContext = dbContext;
        _tenantStoreDbContext = tenantStoreDbContext;
        _stripeConnectService = stripeConnectService;
        _stripePlatformService = stripePlatformService;
        _tenantConfigService = tenantConfigService;
    }

    /// <summary>
    /// Step 1: Create a new signup session when a plan is selected
    /// </summary>
    public async Task<SignupSession> StartSignupSessionAsync(
        string platformPlanId,
        string email,
        CancellationToken cancellationToken = default)
    {
        // Validate plan exists and is active
        var plan = await _dbContext.PlatformPlans
            .FirstOrDefaultAsync(p => p.Id == platformPlanId && p.IsActive, cancellationToken);
        
        if (plan == null)
        {
            throw new InvalidOperationException($"Platform plan '{platformPlanId}' not found or inactive.");
        }

        // Check if there's an existing session for this email that's not completed
        var existingSession = await _dbContext.SignupSessions
            .Where(s => s.Email == email && s.State != SignupSessionState.Completed && s.State != SignupSessionState.Expired)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingSession != null && existingSession.ExpiresAt > DateTime.UtcNow)
        {
            // Return existing session if still valid
            existingSession.LastActivityAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return existingSession;
        }

        // Create new signup session
        var session = new SignupSession
        {
            Id = Guid.NewGuid().ToString(),
            PlatformPlanId = platformPlanId,
            Email = email,
            State = SignupSessionState.Started,
            ExpiresAt = DateTime.UtcNow.AddHours(24), // 24-hour expiration
            LastActivityAt = DateTime.UtcNow,
            TenantId = null // Will be set when tenant is created in Step 3
        };

        _dbContext.SignupSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return session;
    }

    /// <summary>
    /// Step 2: Create admin user and associate with signup session
    /// </summary>
    public async Task<User> CreateAdminUserAsync(
        string signupSessionId,
        string email,
        string firstName,
        string lastName,
        string password, // TODO: Will be hashed when Identity is implemented
        CancellationToken cancellationToken = default)
    {
        var session = await ValidateSessionAsync(signupSessionId, SignupSessionState.Started, cancellationToken);

        // Update session with email if it changed (in case it was a placeholder)
        if (session.Email != email)
        {
            session.Email = email;
        }

        // Check if email already exists
        var existingUser = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        
        if (existingUser != null)
        {
            throw new InvalidOperationException($"User with email '{email}' already exists.");
        }

        // Create admin user (without tenant yet - will be associated in step 3)
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Role = UserRoles.Admin,
            IsActive = true,
            TenantId = null!, // Will be set when tenant is created
            LocationId = null // Will be set later during onboarding
        };

        _dbContext.Users.Add(user);

        // Update session
        session.UserId = user.Id;
        session.FirstName = firstName;
        session.LastName = lastName;
        session.State = SignupSessionState.UserCreated;
        session.LastActivityAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return user;
    }

    /// <summary>
    /// Step 3: Create tenant in PendingActivation status
    /// </summary>
    public async Task<Tenant> CreateTenantAsync(
        string signupSessionId,
        string organizationName,
        string subdomain,
        CancellationToken cancellationToken = default)
    {
        var session = await ValidateSessionAsync(signupSessionId, SignupSessionState.UserCreated, cancellationToken);

        if (session.UserId == null)
        {
            throw new InvalidOperationException("User must be created before tenant.");
        }

        // Validate subdomain is unique and valid
        subdomain = subdomain.ToLowerInvariant().Trim();
        if (string.IsNullOrWhiteSpace(subdomain) || subdomain.Length < 3)
        {
            throw new InvalidOperationException("Subdomain must be at least 3 characters.");
        }

        var existingTenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Subdomain == subdomain, cancellationToken);
        
        if (existingTenant != null)
        {
            throw new InvalidOperationException($"Subdomain '{subdomain}' is already taken.");
        }

        // Create tenant in PendingActivation status
        var tenant = new Tenant
        {
            Id = Guid.NewGuid().ToString(),
            Name = organizationName,
            Subdomain = subdomain,
            Status = TenantStatus.PendingActivation,
            ConfigJson = GetDefaultConfig(),
        };

        _dbContext.Tenants.Add(tenant);

        // Associate user with tenant
        var user = await _dbContext.Users.FindAsync([session.UserId], cancellationToken);
        if (user != null)
        {
            user.TenantId = tenant.Id;
        }

        // Update session
        session.TenantId = tenant.Id;
        session.OrganizationName = organizationName;
        session.Subdomain = subdomain;
        session.State = SignupSessionState.TenantCreated;
        session.LastActivityAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Now create the tenant in the Tenant Store database as well
        var tenantStoreEntry = new ClubTenantInfo(
            tenant.Id,
            tenant.Subdomain,
            tenant.Name
        );
        _tenantStoreDbContext.TenantInfo.Add(tenantStoreEntry);
        await _tenantStoreDbContext.SaveChangesAsync(cancellationToken);

        return tenant;
    }

    /// <summary>
    /// Step 4: Initialize Stripe billing and get checkout URL
    /// </summary>
    public async Task<string> InitializeBillingAsync(
        string signupSessionId,
        string successUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default)
    {
        var session = await ValidateSessionAsync(signupSessionId, SignupSessionState.TenantCreated, cancellationToken);

        if (session.TenantId == null || session.PlatformPlan.StripePriceId == null)
        {
            throw new InvalidOperationException("Tenant and platform plan price must be set.");
        }

        // Create Stripe checkout session
        var checkoutSession = await _stripePlatformService.CreateCheckoutSessionAsync(
            session.PlatformPlan.StripePriceId,
            successUrl,
            cancelUrl,
            session.Email,
            new Dictionary<string, string>
            {
                { SIGNUP_SESSION_ID_KEY, session.Id },
                { TENANT_ID_KEY, session.TenantId },
                { PLATFORM_PLAN_ID_KEY, session.PlatformPlanId }
            },
            cancellationToken
        );

        // Update session
        session.StripeCheckoutSessionId = checkoutSession.Id;
        session.State = SignupSessionState.BillingStarted;
        session.LastActivityAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return checkoutSession.Url;
    }

    /// <summary>
    /// Step 5: Activate tenant after successful payment (called by webhook)
    /// This is the ONLY way a tenant becomes Active - via confirmed payment webhook
    /// </summary>
    public async Task ActivateTenantAsync(
        string stripeSubscriptionId,
        string stripeCustomerId,
        DateTime currentPeriodStart,
        DateTime currentPeriodEnd,
        string? stripeCheckoutSessionId = null,
        string? signupSessionId = null,
        CancellationToken cancellationToken = default)
    {
        SignupSession? session = null;

        var sessionQuery = _dbContext.SignupSessions
            .Include(s => s.Tenant)
            .Include(s => s.PlatformPlan)
            .AsQueryable();

        if (!string.IsNullOrEmpty(signupSessionId))
        {
            session = await sessionQuery.FirstOrDefaultAsync(s => s.Id == signupSessionId, cancellationToken);
        }
        else if (!string.IsNullOrEmpty(stripeCheckoutSessionId))
        {
            session = await sessionQuery.FirstOrDefaultAsync(s => s.StripeCheckoutSessionId == stripeCheckoutSessionId, cancellationToken);
        }

        if (session == null)
        {
            throw new InvalidOperationException($"Signup session with Id '{signupSessionId}' or Stripe session '{stripeCheckoutSessionId}' not found.");
        }

        if (session.TenantId == null)
        {
            throw new InvalidOperationException("Tenant must be created before activation.");
        }

        // This method is idempotent - safe to call multiple times
        if (session.State == SignupSessionState.Completed)
        {
            return; // Already activated
        }

        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Create or update tenant subscription
            var subscription = await _dbContext.TenantSubscriptions
                .FirstOrDefaultAsync(ts => ts.TenantId == session.TenantId, cancellationToken);

            if (subscription == null)
            {
                subscription = new TenantSubscription
                {
                    Id = Guid.NewGuid().ToString(),
                    TenantId = session.TenantId,
                    PlatformPlanId = session.PlatformPlanId
                };
                _dbContext.TenantSubscriptions.Add(subscription);
            }

            subscription.StripeCustomerId = stripeCustomerId;
            subscription.StripeSubscriptionId = stripeSubscriptionId;
            subscription.Status = "active";
            subscription.CurrentPeriodStart = currentPeriodStart;
            subscription.CurrentPeriodEnd = currentPeriodEnd;
            subscription.WillRenew = true;

            // Activate tenant - THIS IS THE CRITICAL STEP
            var tenant = session.Tenant;
            if (tenant == null)
            {
                throw new InvalidOperationException("Tenant not found in session.");
            }
            tenant.Status = TenantStatus.Active;

            // Update session
            session.State = SignupSessionState.Completed;
            session.CompletedAt = DateTime.UtcNow;
            session.LastActivityAt = DateTime.UtcNow;

            // TODO: Create default location for tenant
            // TODO: Set user's LocationId to the default location
            // TODO: Send welcome email to admin

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Handle billing failure (called by webhook)
    /// Tenant remains in PendingActivation - user can retry payment
    /// </summary>
    public async Task HandleBillingFailureAsync(
        string stripeCheckoutSessionId,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.SignupSessions
            .FirstOrDefaultAsync(s => s.StripeCheckoutSessionId == stripeCheckoutSessionId, cancellationToken);

        if (session == null)
        {
            // Webhook for unknown session - log but don't throw
            return;
        }

        // Update session with error - user can resume and retry
        session.ErrorMessage = errorMessage;
        session.LastActivityAt = DateTime.UtcNow;
        // Keep state as BillingStarted so user can retry

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Tenant remains in PendingActivation status
        // User can generate a new checkout session to retry payment
    }

    /// <summary>
    /// Get signup session by ID (for resuming abandoned sessions)
    /// </summary>
    public async Task<SignupSession?> GetSignupSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.SignupSessions
            .Include(s => s.PlatformPlan)
            .Include(s => s.User)
            .Include(s => s.Tenant)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
    }

    /// <summary>
    /// Resume an abandoned signup session
    /// </summary>
    public async Task<SignupSession> ResumeSignupSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSignupSessionAsync(sessionId, cancellationToken);

        if (session == null)
        {
            throw new InvalidOperationException($"Signup session '{sessionId}' not found.");
        }

        if (session.ExpiresAt < DateTime.UtcNow)
        {
            session.State = SignupSessionState.Expired;
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Signup session has expired.");
        }

        if (session.State == SignupSessionState.Completed)
        {
            throw new InvalidOperationException("Signup session is already completed.");
        }

        session.LastActivityAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return session;
    }

    /// <summary>
    /// Sets up Stripe Connect for an existing tenant (existing functionality)
    /// </summary>
    public async Task<string> SetupStripeConnectAsync(
        string tenantId,
        string adminEmail,
        string refreshUrl,
        string returnUrl,
        CancellationToken cancellationToken = default)
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
            tenantConfig,
            cancellationToken
        );

        // Save the account ID to the tenant
        tenant.StripeAccountId = accountId;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Create and return onboarding link
        var onboardingUrl = await _stripeConnectService.CreateAccountLinkAsync(
            accountId,
            refreshUrl,
            returnUrl,
            cancellationToken
        );

        return onboardingUrl;
    }

    // Private helper methods

    private async Task<SignupSession> ValidateSessionAsync(
        string sessionId,
        string expectedState,
        CancellationToken cancellationToken)
    {
        var session = await _dbContext.SignupSessions
            .Include(s => s.PlatformPlan)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session == null)
        {
            throw new InvalidOperationException($"Signup session '{sessionId}' not found.");
        }

        if (session.ExpiresAt < DateTime.UtcNow)
        {
            session.State = SignupSessionState.Expired;
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Signup session has expired.");
        }

        if (session.State != expectedState)
        {
            throw new InvalidOperationException($"Invalid session state. Expected: {expectedState}, Actual: {session.State}");
        }

        session.LastActivityAt = DateTime.UtcNow;
        return session;
    }

    /// <summary>
    /// Get default configuration JSON string for new tenant.
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
    }
}