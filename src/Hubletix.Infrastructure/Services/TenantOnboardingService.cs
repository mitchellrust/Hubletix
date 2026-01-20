using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Hubletix.Core.Entities;
using Hubletix.Core.Constants;
using Hubletix.Core.Enums;
using Hubletix.Infrastructure.Persistence;
using Hubletix.Core.Models;
using System.Text.Json;

namespace Hubletix.Infrastructure.Services;

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
    /// Step 2: Create admin user (Identity + PlatformUser) and associate with signup session
    /// </summary>
    Task<(User identityUser, PlatformUser platformUser)> CreateAdminUserAsync(
        string signupSessionId,
        string email,
        string firstName,
        string lastName,
        string password,
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
        string stripeAccountId,
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
    /// Refresh signup session by checking Stripe checkout session status.
    /// If payment is complete, activates tenant idempotently. Used to reconcile missed webhooks.
    /// </summary>
    Task RefreshPlatformSubscriptionAsync(
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

    /// <summary>
    /// Get an account update link for an existing tenant
    /// </summary>
    Task<string> GetAccountUpdateLinkAsync(
        string tenantId,
        string refreshUrl,
        string returnUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refresh Stripe account information for a tenant
    /// </summary>
    Task<Tenant> RefreshStripeAccountAsync(
        string tenantId,
        CancellationToken cancellationToken = default);
}

public class TenantOnboardingService : ITenantOnboardingService
{
    private readonly AppDbContext _dbContext;
    private readonly TenantStoreDbContext _tenantStoreDbContext;
    private readonly UserManager<User> _userManager;
    private readonly IStripeConnectService _stripeConnectService;
    private readonly IStripePlatformService _stripePlatformService;
    private readonly ITenantConfigService _tenantConfigService;
    private readonly ILogger<TenantOnboardingService> _logger;
    private const string SIGNUP_SESSION_ID_KEY = "signup_session_id";
    private const string TENANT_ID_KEY = "tenant_id";
    private const string PLATFORM_PLAN_ID_KEY = "plan_id";

    public TenantOnboardingService(
        AppDbContext dbContext,
        TenantStoreDbContext tenantStoreDbContext,
        UserManager<User> userManager,
        IStripeConnectService stripeConnectService,
        IStripePlatformService stripePlatformService,
        ITenantConfigService tenantConfigService,
        ILogger<TenantOnboardingService> logger)
    {
        _dbContext = dbContext;
        _tenantStoreDbContext = tenantStoreDbContext;
        _userManager = userManager;
        _stripeConnectService = stripeConnectService;
        _stripePlatformService = stripePlatformService;
        _tenantConfigService = tenantConfigService;
        _logger = logger;
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
    /// Step 2: Create admin user (Identity + PlatformUser) and associate with signup session
    /// </summary>
    public async Task<(User identityUser, PlatformUser platformUser)> CreateAdminUserAsync(
        string signupSessionId,
        string email,
        string firstName,
        string lastName,
        string password,
        CancellationToken cancellationToken = default)
    {
        var session = await ValidateSessionAsync(signupSessionId, SignupSessionState.Started, cancellationToken);

        // Update session with email if it changed (in case it was a placeholder)
        if (session.Email != email)
        {
            session.Email = email;
        }

        // Check if email already exists
        var existingUser = await _userManager.FindByEmailAsync(email);
        
        if (existingUser != null)
        {
            throw new InvalidOperationException($"User with email '{email}' already exists.");
        }

        // Create Identity user (authentication layer) with password hashing
        var identityUser = new User
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true // Auto-confirm for now
        };

        var result = await _userManager.CreateAsync(identityUser, password);
        
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create user: {errors}");
        }

        // Create PlatformUser (domain layer)
        var platformUser = new PlatformUser
        {
            IdentityUserId = identityUser.Id,
            FirstName = firstName,
            LastName = lastName,
            IsActive = true,
        };

        _dbContext.PlatformUsers.Add(platformUser);

        // Update session
        session.UserId = identityUser.Id; // Store IdentityUser ID for auth compatibility
        session.FirstName = firstName;
        session.LastName = lastName;
        session.State = SignupSessionState.UserCreated;
        session.LastActivityAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return (identityUser, platformUser);
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

        // Create the tenant in the Tenant Store database first, ensuring this succeeds before adding to main DB
        try
        {
            var tenantStoreEntry = new ClubTenantInfo(
                tenant.Id,
                tenant.Subdomain,
                tenant.Name
            );
            _tenantStoreDbContext.TenantInfo.Add(tenantStoreEntry);
            await _tenantStoreDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to create tenant '{tenant.Name}' in Tenant Store. Tenant was not created.", 
                ex);
        }

        _dbContext.Tenants.Add(tenant);

        // Get PlatformUser for this Identity user and create TenantUser with owner role
        var platformUser = await _dbContext.PlatformUsers
            .FirstOrDefaultAsync(pu => pu.IdentityUserId == session.UserId, cancellationToken);
        
        if (platformUser == null)
        {
            throw new InvalidOperationException("PlatformUser not found for signup session.");
        }

        // Create TenantUser membership with Admin role and IsOwner flag
        var tenantUser = new TenantUser
        {
            PlatformUserId = platformUser.Id,
            TenantId = tenant.Id,
            Role = TenantRole.Admin,
            Status = TenantUserStatus.Active,
            IsOwner = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "System"
        };

        _dbContext.TenantUsers.Add(tenantUser);

        // Update session
        session.TenantId = tenant.Id;
        session.OrganizationName = organizationName;
        session.Subdomain = subdomain;
        session.State = SignupSessionState.TenantCreated;
        session.LastActivityAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

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

        // If there's an existing checkout session, return its URL
        if (!string.IsNullOrEmpty(session.StripeCheckoutSessionId))
        {
            var existingStripeSession = await _stripePlatformService.GetCheckoutSessionAsync(
                session.StripeCheckoutSessionId,
                cancellationToken
            );
            if (existingStripeSession != null)
            {
                return existingStripeSession.Url;
            }
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
        string stripeAccountId,
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
            tenant.StripeAccountId = stripeAccountId;

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
        if (string.IsNullOrEmpty(sessionId))
        {
            return null;
        }
        
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
    /// Supports resumption - can regenerate account links for incomplete onboarding
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

        // Check if onboarding is already completed
        if (tenant.StripeOnboardingState == StripeOnboardingState.Completed)
        {
            throw new InvalidOperationException("Stripe Connect onboarding is already completed.");
        }

        string accountId;
        // Parse tenant configuration
        var tenantConfig = ParseTenantConfig(tenant.ConfigJson);

        // If no Stripe account exists yet, create one
        if (string.IsNullOrEmpty(tenant.StripeAccountId))
        {
            // Create Stripe Connect account
            accountId = await _stripeConnectService.CreateConnectAccountAsync(
                tenant.Id,
                tenant.Name,
                adminEmail,
                tenantConfig,
                cancellationToken
            );

            // Save the account ID and update state to AccountCreated
            tenant.StripeAccountId = accountId;
            tenant.StripeOnboardingState = StripeOnboardingState.AccountCreated;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            // Update Stripe Connect account to make it a merchant.
            accountId = await _stripeConnectService.UpdateConnectAccountAsync(
                tenant.Id,
                tenant.StripeAccountId,
                tenant.Name,
                adminEmail,
                tenantConfig,
                cancellationToken
            );
        }

        // Update state to OnboardingStarted (user is clicking the link)
        tenant.StripeOnboardingState = StripeOnboardingState.OnboardingStarted;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Create and return onboarding link (regenerated on-demand)
        var onboardingUrl = await _stripeConnectService.CreateAccountLinkAsync(
            accountId,
            refreshUrl,
            returnUrl,
            cancellationToken
        );

        return onboardingUrl;
    }

    public async Task<string> GetAccountUpdateLinkAsync(
        string tenantId,
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

        // If no Stripe account ID, cannot create update link
        if (string.IsNullOrEmpty(tenant.StripeAccountId))
        {
            throw new InvalidOperationException($"Tenant with ID '{tenantId}' does not have a Stripe account associated.");
        }

        // Create and return update link (regenerated on-demand)
        var updateUrl = await _stripeConnectService.CreateAccountLinkAsync(
            tenant.StripeAccountId,
            refreshUrl,
            returnUrl,
            cancellationToken
        );

        return updateUrl;
    }

    public async Task<Tenant> RefreshStripeAccountAsync(
        string tenantId,
        CancellationToken cancellationToken = default
    )
    {
        // Load tenant (from central DB)
        var tenant = await _tenantConfigService.GetTenantAsync(tenantId);
        if (tenant == null)
        {
            throw new InvalidOperationException($"Tenant '{tenantId}' not found.");
        }

        // Ensure tenant has a Stripe account
        if (string.IsNullOrEmpty(tenant.StripeAccountId))
        {
            throw new InvalidOperationException("Tenant does not have a Stripe Connect account set up.");
        }

        // Retrieve the Stripe account information
        var account = await _stripeConnectService.GetConnectAccountAsync(
            tenant.StripeAccountId,
            cancellationToken
        );
        if (account == null)
        {
            throw new InvalidOperationException("Stripe Connect account not found.");
        }

        var previousChargesEnabled = tenant.ChargesEnabled;
        tenant.ChargesEnabled = account.ChargesEnabled;
        tenant.PayoutsEnabled = account.PayoutsEnabled;
        tenant.DetailsSubmitted = account.DetailsSubmitted;

        // Check if onboarding just completed (charges enabled for first time)
        if (account.ChargesEnabled && !previousChargesEnabled)
        {
            tenant.StripeOnboardingState = StripeOnboardingState.Completed;
            tenant.OnboardingCompletedAt = DateTime.UtcNow;
        }

        // Order of checking matters here - we want to show the most urgent status
        if (account.Requirements.PendingVerification.Count > 0)
        {
            tenant.StripeAccountRequirementsStatus = StripeAccountRequirementsStatus.PendingVerification;
        }
        else if (account.Requirements.PastDue.Count > 0)
        {
            tenant.StripeAccountRequirementsStatus = StripeAccountRequirementsStatus.PastDue;
        }
        else if (account.Requirements.CurrentlyDue.Count > 0)
        {
            tenant.StripeAccountRequirementsStatus = StripeAccountRequirementsStatus.CurrentlyDue;
        }
        else if (account.Requirements.EventuallyDue.Count > 0)
        {
            tenant.StripeAccountRequirementsStatus = StripeAccountRequirementsStatus.EventuallyDue;
        }
        else
        {
            tenant.StripeAccountRequirementsStatus = StripeAccountRequirementsStatus.None;
        }

        // Check if anything actually changed so we can key off of that
        int numChanges = await _dbContext.SaveChangesAsync(cancellationToken);
        if (numChanges > 0)
        {
            // Invalidate cache
            _tenantConfigService.InvalidateCache(tenantId);
        }

        return tenant;
    }

    /// <summary>
    /// Refresh signup session by checking Stripe checkout session status.
    /// If payment is complete, activates tenant idempotently. Used to reconcile missed webhooks.
    /// </summary>
    public async Task RefreshPlatformSubscriptionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        // Load signup session with related entities
        var session = await _dbContext.SignupSessions
            .Include(s => s.Tenant)
            .Include(s => s.PlatformPlan)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session == null)
        {
            _logger.LogWarning("Signup session not found for refresh: {SessionId}", sessionId);
            return;
        }

        // Skip if already completed
        if (session.State == SignupSessionState.Completed)
        {
            _logger.LogDebug("Signup session already completed, skipping refresh: {SessionId}", sessionId);
            return;
        }

        // Tenant and checkout session must exist to check Stripe
        if (session.Tenant == null)
        {
            _logger.LogDebug("Tenant not yet created, skipping refresh: {SessionId}", sessionId);
            return;
        }

        if (string.IsNullOrEmpty(session.StripeCheckoutSessionId))
        {
            _logger.LogDebug("Stripe checkout session not yet created, skipping refresh: {SessionId}", sessionId);
            return;
        }

        _logger.LogInformation("Refreshing platform subscription status from Stripe: SessionId={SessionId}, CheckoutSessionId={CheckoutSessionId}",
            sessionId, session.StripeCheckoutSessionId);

        try
        {
            // Fetch checkout session from Stripe
            var checkoutSession = await _stripePlatformService.GetCheckoutSessionAsync(
                session.StripeCheckoutSessionId,
                cancellationToken);

            if (checkoutSession == null)
            {
                _logger.LogWarning("Stripe checkout session not found: {CheckoutSessionId}", session.StripeCheckoutSessionId);
                return;
            }

            // Check if checkout is complete and has a subscription
            if (checkoutSession.Status != "complete" || checkoutSession.PaymentStatus != "paid")
            {
                _logger.LogDebug("Checkout session not yet complete: SessionId={SessionId}, Status={Status}, PaymentStatus={PaymentStatus}",
                    sessionId, checkoutSession.Status, checkoutSession.PaymentStatus);
                return;
            }

            if (checkoutSession.Mode != "subscription" || string.IsNullOrEmpty(checkoutSession.SubscriptionId))
            {
                _logger.LogWarning("Checkout session is not a subscription or missing subscription ID: SessionId={SessionId}, Mode={Mode}",
                    sessionId, checkoutSession.Mode);
                return;
            }

            _logger.LogInformation("Checkout session is complete: SessionId={SessionId}, SubscriptionId={SubscriptionId}, CustomerId={CustomerId}",
                sessionId, checkoutSession.SubscriptionId, checkoutSession.CustomerId);

            // Fetch the subscription to get period dates
            var subscription = await _stripePlatformService.GetSubscriptionAsync(
                checkoutSession.SubscriptionId,
                cancellationToken);

            if (subscription == null)
            {
                _logger.LogWarning("Subscription not found in Stripe: {SubscriptionId}", checkoutSession.SubscriptionId);
                return;
            }

            // Get all active platform plan price IDs from database (matching webhook logic)
            var platformPlanPriceIds = await _dbContext.PlatformPlans
                .Where(p => !string.IsNullOrEmpty(p.StripePriceId))
                .Select(p => p.StripePriceId!)
                .ToListAsync(cancellationToken);

            var dbPriceSet = new HashSet<string>(platformPlanPriceIds);

            // Find subscription item with matching platform plan price
            var matchingItem = subscription.Items.Data
                .FirstOrDefault(i => i.Price != null && dbPriceSet.Contains(i.Price.Id));

            if (matchingItem == null)
            {
                _logger.LogWarning("No matching platform plan found in subscription: SessionId={SessionId}, SubscriptionId={SubscriptionId}",
                    sessionId, subscription.Id);
                return;
            }

            _logger.LogInformation("Found matching platform subscription in Stripe: SessionId={SessionId}, SubscriptionId={SubscriptionId}, Status={Status}",
                sessionId, subscription.Id, subscription.Status);

            // Activate tenant idempotently using existing activation logic
            await ActivateTenantAsync(
                subscription.Id,
                subscription.CustomerId,
                subscription.CustomerAccount,
                matchingItem.CurrentPeriodStart,
                matchingItem.CurrentPeriodEnd,
                signupSessionId: sessionId,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Successfully refreshed and activated tenant from Stripe: SessionId={SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing platform subscription from Stripe: SessionId={SessionId}", sessionId);
            // Don't throw - this is a best-effort reconciliation
        }
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