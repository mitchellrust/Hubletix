using Microsoft.AspNetCore.Mvc;
using Stripe;
using Microsoft.Extensions.Options;
using Hubletix.Core.Models;
using Hubletix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Hubletix.Core.Constants;
using Hubletix.Infrastructure.Services;

namespace Hubletix.Api.Controllers;

/// <summary>
/// Webhook controller for handling Stripe Connect account events.
/// This handles tenant-level Stripe Connect account updates (onboarding completion, capabilities enabled, etc.)
/// </summary>
[ApiController]
[Route("api/webhooks/stripe/connect")]
public class StripeConnectWebhookController : ControllerBase
{
    private readonly ITenantConfigService _tenantConfigService;
    private readonly ILogger<StripeConnectWebhookController> _logger;
    private readonly AppDbContext _dbContext;
    private readonly string _webhookSecret;

    public StripeConnectWebhookController(
        ITenantConfigService tenantConfigService,
        ILogger<StripeConnectWebhookController> logger,
        IOptions<StripeSettings> stripeSettings,
        AppDbContext dbContext)
    {
        _tenantConfigService = tenantConfigService;
        _logger = logger;
        _webhookSecret = stripeSettings.Value.Connect.WebhookSecret;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Handle incoming Stripe Connect webhook events.
    /// Primary use case: Track when tenant Stripe accounts complete onboarding and have charges enabled.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> HandleWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var signatureHeader = Request.Headers["Stripe-Signature"];

        try
        {
            // Verify webhook signature to ensure it came from Stripe
            var stripeEvent = EventUtility.ConstructEvent(
                json,
                signatureHeader,
                _webhookSecret,
                throwOnApiVersionMismatch: false
            );

            _logger.LogInformation(
                "Received Stripe Connect webhook: {EventType} {EventId}",
                stripeEvent.Type,
                stripeEvent.Id
            );

            // Handle different event types
            switch (stripeEvent.Type)
            {
                case EventTypes.AccountUpdated:
                    await HandleAccountUpdated(stripeEvent);
                    break;

                default:
                    _logger.LogInformation(
                        "Unhandled Stripe Connect webhook event type: {EventType}",
                        stripeEvent.Type
                    );
                    break;
            }

            return Ok();
        }
        catch (StripeException ex)
        {
            _logger.LogError(
                ex,
                "Stripe webhook signature verification failed: {Message}",
                ex.Message
            );
            return BadRequest();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing Stripe Connect webhook: {Message}",
                ex.Message
            );
            // Return 200 to prevent Stripe from retrying
            // We've logged the error and can investigate manually
            return Ok();
        }
    }

    /// <summary>
    /// Handle account.updated event - fires when Stripe Connect account details change
    /// Updates tenant onboarding state and capability flags
    /// </summary>
    private async Task HandleAccountUpdated(Event stripeEvent)
    {
        var account = stripeEvent.Data.Object as Account;
        if (account == null)
        {
            _logger.LogWarning("Account.updated event received without account data");
            return;
        }

        _logger.LogInformation(
            "Processing account.updated for Stripe account {AccountId}",
            account.Id
        );

        // Find tenant by Stripe account ID
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.StripeAccountId == account.Id);

        if (tenant == null)
        {
            _logger.LogWarning(
                "No tenant found for Stripe account {AccountId}",
                account.Id
            );
            return;
        }

        // Update capability flags
        var previousChargesEnabled = tenant.ChargesEnabled;
        tenant.ChargesEnabled = account.ChargesEnabled;
        tenant.PayoutsEnabled = account.PayoutsEnabled;
        tenant.DetailsSubmitted = account.DetailsSubmitted;

        // Check if onboarding just completed (charges enabled for first time)
        if (account.ChargesEnabled && !previousChargesEnabled)
        {
            tenant.StripeOnboardingState = StripeOnboardingState.Completed;
            tenant.OnboardingCompletedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Stripe Connect onboarding completed for tenant {TenantId} (account {AccountId})",
                tenant.Id,
                account.Id
            );
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
        int numChanges = await _dbContext.SaveChangesAsync();
        if (numChanges > 0)
        {
            // Invalidate cache
            _tenantConfigService.InvalidateCache(tenant.Id);
            _logger.LogInformation(
                "Updated tenant {TenantId}: ChargesEnabled={ChargesEnabled}, PayoutsEnabled={PayoutsEnabled}, DetailsSubmitted={DetailsSubmitted}, State={State}",
                tenant.Id,
                tenant.ChargesEnabled,
                tenant.PayoutsEnabled,
                tenant.DetailsSubmitted,
                tenant.StripeOnboardingState
            );
        }
        else
        {
            _logger.LogInformation(
                "No changes for tenant [{TenantId}] on account.updated event",
                tenant.Id
            );
        }
    }
}
