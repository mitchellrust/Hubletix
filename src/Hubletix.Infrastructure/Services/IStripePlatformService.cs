using Stripe;
using Stripe.Checkout;

namespace Hubletix.Infrastructure.Services;

/// <summary>
/// Service for handling direct Stripe operations on the platform account.
/// Used for collecting payments from tenants to the platform (e.g., subscription fees, setup fees).
/// </summary>
public interface IStripePlatformService
{
    /// <summary>
    /// Creates a Checkout Session for a platform payment (e.g., tenant subscribing to platform).
    /// </summary>
    /// <param name="priceId">The Stripe Price ID for the platform product</param>
    /// <param name="successUrl">URL to redirect after successful payment</param>
    /// <param name="cancelUrl">URL to redirect if payment is cancelled</param>
    /// <param name="customerEmail">Customer's email address</param>
    /// <param name="metadata">Additional metadata to attach to the session</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The Checkout Session with URL</returns>
    Task<Session> CreateCheckoutSessionAsync(
        string priceId,
        string successUrl,
        string cancelUrl,
        string? customerEmail,
        Dictionary<string, string>? metadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a Checkout Session from the platform account.
    /// </summary>
    /// <param name="sessionId">The Checkout Session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The Checkout Session</returns>
    Task<Session> GetCheckoutSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a customer in the platform Stripe account.
    /// </summary>
    /// <param name="email">Customer email</param>
    /// <param name="name">Customer name</param>
    /// <param name="metadata">Additional metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created Stripe Customer ID</returns>
    Task<string> CreateCustomerAsync(
        string email,
        string? name,
        Dictionary<string, string>? metadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a subscription for a customer on the platform account.
    /// </summary>
    /// <param name="customerId">The Stripe Customer ID</param>
    /// <param name="priceId">The Stripe Price ID</param>
    /// <param name="metadata">Additional metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created Stripe Subscription ID</returns>
    Task<string> CreateSubscriptionAsync(
        string customerId,
        string priceId,
        Dictionary<string, string>? metadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a Subscription from the platform account.
    /// </summary>
    /// <param name="subscriptionId">The Subscription ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The Subscription</returns>
    Task<Subscription> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all subscriptions for a customer from the platform account.
    /// </summary>
    /// <param name="customerId">The Stripe Customer ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of subscriptions for the customer</returns>
    Task<List<Subscription>> GetCustomerSubscriptionsAsync(string customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a subscription on the platform account.
    /// </summary>
    /// <param name="subscriptionId">The Stripe Subscription ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CancelSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a platform product (e.g., "Basic Plan", "Premium Plan").
    /// </summary>
    /// <param name="name">Product name</param>
    /// <param name="description">Product description</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created Stripe Product ID</returns>
    Task<string> CreateProductAsync(string name, string? description, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a price for a platform product.
    /// </summary>
    /// <param name="productId">The Stripe Product ID</param>
    /// <param name="amountInCents">Price amount in cents</param>
    /// <param name="currency">Currency code (e.g., "usd")</param>
    /// <param name="interval">Billing interval: "month", "year", or null for one-time</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created Stripe Price ID</returns>
    Task<string> CreatePriceAsync(
        string productId,
        long amountInCents,
        string currency,
        string? interval,
        CancellationToken cancellationToken = default);
}
