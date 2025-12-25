using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using ClubManagement.Infrastructure.Services;
using Microsoft.Extensions.Options;
using ClubManagement.Core.Models;

namespace ClubManagement.Api.Controllers;

/// <summary>
/// Webhook controller for handling Stripe platform payment events.
/// This handles billing for tenant subscriptions to the platform.
/// </summary>
[ApiController]
[Route("api/webhooks/stripe/platform")]
public class StripePlatformWebhookController : ControllerBase
{
    private readonly ITenantOnboardingService _onboardingService;
    private readonly ILogger<StripePlatformWebhookController> _logger;
    private readonly string _webhookSecret;

    public StripePlatformWebhookController(
        ITenantOnboardingService onboardingService,
        ILogger<StripePlatformWebhookController> logger,
        IOptions<StripeSettings> stripeSettings)
    {
        _onboardingService = onboardingService;
        _logger = logger;
        _webhookSecret = stripeSettings.Value.Platform.WebhookSecret;
    }

    /// <summary>
    /// Handle incoming Stripe webhook events.
    /// This is the ONLY source of truth for tenant activation.
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
                "Received Stripe webhook: {EventType} {EventId}",
                stripeEvent.Type,
                stripeEvent.Id
            );

            // Handle different event types
            switch (stripeEvent.Type)
            {
                case "checkout.session.completed":
                    await HandleCheckoutSessionCompleted(stripeEvent);
                    break;

                case "invoice.paid":
                    await HandleInvoicePaid(stripeEvent);
                    break;

                case "invoice.payment_failed":
                    await HandleInvoicePaymentFailed(stripeEvent);
                    break;

                case "customer.subscription.updated":
                    await HandleSubscriptionUpdated(stripeEvent);
                    break;

                case "customer.subscription.deleted":
                    await HandleSubscriptionDeleted(stripeEvent);
                    break;

                default:
                    _logger.LogInformation("Unhandled event type: {EventType}", stripeEvent.Type);
                    break;
            }

            return Ok();
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe webhook signature verification failed");
            return BadRequest();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook");
            // Return 200 to prevent Stripe from retrying
            // We've logged the error and can investigate manually
            return Ok();
        }
    }

    /// <summary>
    /// Handle checkout.session.completed event.
    /// This fires when the checkout session is completed (payment collected).
    /// </summary>
    private async Task HandleCheckoutSessionCompleted(Event stripeEvent)
    {
        var session = stripeEvent.Data.Object as Session;
        if (session == null)
        {
            _logger.LogWarning("Checkout session is null in webhook event");
            return;
        }

        _logger.LogInformation(
            "Checkout completed: SessionId={SessionId}, SubscriptionId={SubscriptionId}",
            session.Id,
            session.SubscriptionId
        );

        // For subscription mode, wait for invoice.paid event
        // This ensures payment is actually processed
        if (session.Mode == "subscription" && session.SubscriptionId != null)
        {
            _logger.LogInformation(
                "Subscription checkout completed, waiting for invoice.paid event. SessionId={SessionId}",
                session.Id
            );
            return;
        }

        // For one-time payments, activate immediately
        if (session.Mode == "payment" && session.PaymentStatus == "paid")
        {
            await ActivateTenantFromSession(session);
        }
    }

    /// <summary>
    /// Handle invoice.paid event.
    /// This is the definitive event for subscription activation.
    /// Fires when the first invoice is successfully paid.
    /// </summary>
    private async Task HandleInvoicePaid(Event stripeEvent)
    {
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice == null)
        {
            _logger.LogWarning("Invoice is null in webhook event");
            return;
        }

        // Only process the first invoice (billing_reason = "subscription_create")
        if (invoice.BillingReason != "subscription_create")
        {
            _logger.LogInformation(
                "Invoice paid but not initial subscription. BillingReason={BillingReason}, InvoiceId={InvoiceId}",
                invoice.BillingReason,
                invoice.Id
            );
            return;
        }

        _logger.LogInformation(
            "Initial subscription invoice paid: InvoiceId={InvoiceId}, SubscriptionId={SubscriptionId}",
            invoice.Id,
            invoice.SubscriptionId
        );

        // Get the checkout session from metadata
        var checkoutSessionId = invoice.Subscription?.Metadata?["checkout_session_id"];
        if (string.IsNullOrEmpty(checkoutSessionId))
        {
            // Fallback: get customer metadata
            checkoutSessionId = invoice.CustomerEmail; // We'll need to look up by email
            _logger.LogWarning(
                "Checkout session ID not in subscription metadata. InvoiceId={InvoiceId}",
                invoice.Id
            );
        }

        if (invoice.Subscription == null)
        {
            _logger.LogError("Subscription is null on invoice. InvoiceId={InvoiceId}", invoice.Id);
            return;
        }

        // Activate tenant
        try
        {
            await _onboardingService.ActivateTenantAsync(
                checkoutSessionId ?? invoice.Id, // Fallback to invoice ID
                invoice.SubscriptionId,
                invoice.CustomerId,
                invoice.Subscription.CurrentPeriodStart,
                invoice.Subscription.CurrentPeriodEnd
            );

            _logger.LogInformation(
                "Tenant activated successfully. SubscriptionId={SubscriptionId}",
                invoice.SubscriptionId
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to activate tenant. SubscriptionId={SubscriptionId}",
                invoice.SubscriptionId
            );
            throw; // Re-throw to trigger webhook retry
        }
    }

    /// <summary>
    /// Handle invoice.payment_failed event.
    /// Marks the signup session with an error.
    /// Tenant remains in PendingActivation.
    /// </summary>
    private async Task HandleInvoicePaymentFailed(Event stripeEvent)
    {
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice == null)
        {
            _logger.LogWarning("Invoice is null in webhook event");
            return;
        }

        _logger.LogWarning(
            "Invoice payment failed: InvoiceId={InvoiceId}, CustomerId={CustomerId}",
            invoice.Id,
            invoice.CustomerId
        );

        // Get checkout session from metadata if available
        var checkoutSessionId = invoice.Subscription?.Metadata?["checkout_session_id"];
        if (!string.IsNullOrEmpty(checkoutSessionId))
        {
            await _onboardingService.HandleBillingFailureAsync(
                checkoutSessionId,
                $"Payment failed: {invoice.PaymentIntent?.LastPaymentError?.Message ?? "Unknown error"}"
            );
        }

        // Tenant remains in PendingActivation - user can retry payment
    }

    /// <summary>
    /// Handle customer.subscription.updated event.
    /// Updates subscription status in our database.
    /// </summary>
    private async Task HandleSubscriptionUpdated(Event stripeEvent)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription == null)
        {
            _logger.LogWarning("Subscription is null in webhook event");
            return;
        }

        _logger.LogInformation(
            "Subscription updated: SubscriptionId={SubscriptionId}, Status={Status}",
            subscription.Id,
            subscription.Status
        );

        // TODO: Update TenantSubscription status in database
        // This would handle status changes like past_due, canceled, etc.
    }

    /// <summary>
    /// Handle customer.subscription.deleted event.
    /// Suspends the tenant.
    /// </summary>
    private async Task HandleSubscriptionDeleted(Event stripeEvent)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription == null)
        {
            _logger.LogWarning("Subscription is null in webhook event");
            return;
        }

        _logger.LogWarning(
            "Subscription deleted: SubscriptionId={SubscriptionId}",
            subscription.Id
        );

        // TODO: Suspend tenant in database
        // Find tenant by subscription ID and set status to Suspended
    }

    /// <summary>
    /// Helper method to activate tenant from checkout session
    /// </summary>
    private async Task ActivateTenantFromSession(Session session)
    {
        if (session.SubscriptionId == null || session.CustomerId == null)
        {
            _logger.LogError(
                "Cannot activate tenant: Missing subscription or customer. SessionId={SessionId}",
                session.Id
            );
            return;
        }

        // Get subscription details
        var subscriptionService = new SubscriptionService();
        var subscription = await subscriptionService.GetAsync(session.SubscriptionId);

        await _onboardingService.ActivateTenantAsync(
            session.Id,
            session.SubscriptionId,
            session.CustomerId,
            subscription.CurrentPeriodStart,
            subscription.CurrentPeriodEnd
        );

        _logger.LogInformation(
            "Tenant activated from checkout session. SessionId={SessionId}",
            session.Id
        );
    }
}
