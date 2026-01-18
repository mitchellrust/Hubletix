using Hubletix.Core.Models;
using Stripe;
using Stripe.Checkout;

namespace Hubletix.Infrastructure.Services;

/// <summary>
/// Service for handling Stripe Connect operations.
/// Manages payment processing for tenants via their connected Stripe accounts.
/// </summary>
public interface IStripeConnectService
{
    /// <summary>
    /// Creates a Stripe Connect account for a tenant.
    /// </summary>
    /// <param name="tenantId">The tenant ID</param>
    /// <param name="name">Name of the tenant, for the Connect account</param>
    /// <param name="email">Email for the Connect account</param>
    /// <param name="tenantConfig">The tenant configuration including settings</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created Stripe account ID</returns>
    Task<string> CreateConnectAccountAsync(string tenantId, string name, string email, TenantConfig tenantConfig, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a Stripe Connect account for a tenant. Most often, this will be
    /// making an existing customer into a connected account, with merchant capabilities.
    /// </summary>
    /// <param name="tenantId">The tenant ID</param>
    /// <param name="stripeAccountId">The existing Stripe account ID</param>
    /// <param name="name">Name of the tenant, for the Connect account</param>
    /// <param name="email">Email for the Connect account</param>
    /// <param name="tenantConfig">The tenant configuration including settings</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created Stripe account ID</returns>
    Task<string> UpdateConnectAccountAsync(string tenantId, string stripeAccountId, string name, string email, TenantConfig tenantConfig, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an account link for onboarding a Connect account.
    /// </summary>
    /// <param name="stripeAccountId">The Stripe Connect account ID</param>
    /// <param name="refreshUrl">URL to redirect if the link expires</param>
    /// <param name="returnUrl">URL to redirect after completion</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The onboarding URL</returns>
    Task<string> CreateAccountLinkAsync(string stripeAccountId, string refreshUrl, string returnUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Stripe Product for a tenant's membership plan.
    /// </summary>
    /// <param name="stripeAccountId">The tenant's Stripe Connect account ID</param>
    /// <param name="name">Product name</param>
    /// <param name="description">Product description</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created Stripe Product ID</returns>
    Task<string> CreateProductAsync(string stripeAccountId, string name, string? description, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Stripe Price for a tenant's membership plan.
    /// </summary>
    /// <param name="stripeAccountId">The tenant's Stripe Connect account ID</param>
    /// <param name="productId">The Stripe Product ID</param>
    /// <param name="amountInCents">Price amount in cents</param>
    /// <param name="currency">Currency code (e.g., "usd")</param>
    /// <param name="interval">Billing interval: "month", "year", or null for one-time</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created Stripe Price ID</returns>
    Task<string> CreatePriceAsync(
        string stripeAccountId,
        string productId,
        long amountInCents,
        string currency,
        string? interval,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Checkout Session for a tenant's product/membership.
    /// </summary>
    /// <param name="stripeAccountId">The tenant's Stripe Connect account ID</param>
    /// <param name="priceId">The Stripe Price ID</param>
    /// <param name="applicationFeeAmountInCents">The application fee amount in cents to charge the tenant</param>
    /// <param name="isRecurring">Whether the price is for a recurring subscription</param
    /// <param name="successUrl">URL to redirect after successful payment</param>
    /// <param name="cancelUrl">URL to redirect if payment is cancelled</param>
    /// <param name="stripeCustomerId">Optional Stripe Customer ID to prefill</param>
    /// <param name="customerEmail">Customer's email address</param>
    /// <param name="metadata">Additional metadata to attach to the session</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The Checkout Session with URL</returns>
    Task<Session> CreateCheckoutSessionAsync(
        string stripeAccountId,
        string priceId,
        long applicationFeeAmountInCents,
        bool isRecurring,
        string successUrl,
        string cancelUrl,
        string? stripeCustomerId,
        string? customerEmail,
        Dictionary<string, string>? metadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a Checkout Session.
    /// </summary>
    /// <param name="stripeAccountId">The tenant's Stripe Connect account ID</param>
    /// <param name="sessionId">The Checkout Session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The Checkout Session</returns>
    Task<Session> GetCheckoutSessionAsync(string stripeAccountId, string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a customer in the tenant's Stripe account.
    /// </summary>
    /// <param name="stripeAccountId">The tenant's Stripe Connect account ID</param>
    /// <param name="email">Customer email</param>
    /// <param name="name">Customer name</param>
    /// <param name="metadata">Additional metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created Stripe Customer ID</returns>
    Task<string> CreateCustomerAsync(
        string stripeAccountId,
        string email,
        string? name,
        Dictionary<string, string>? metadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a subscription for a customer.
    /// </summary>
    /// <param name="stripeAccountId">The tenant's Stripe Connect account ID</param>
    /// <param name="customerId">The Stripe Customer ID</param>
    /// <param name="priceId">The Stripe Price ID</param>
    /// <param name="metadata">Additional metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created Stripe Subscription ID</returns>
    Task<string> CreateSubscriptionAsync(
        string stripeAccountId,
        string customerId,
        string priceId,
        Dictionary<string, string>? metadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a subscription.
    /// </summary>
    /// <param name="stripeAccountId">The tenant's Stripe Connect account ID</param>
    /// <param name="subscriptionId">The Stripe Subscription ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CancelSubscriptionAsync(string stripeAccountId, string subscriptionId, CancellationToken cancellationToken = default);
}
