using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using Hubletix.Core.Models;

namespace Hubletix.Infrastructure.Services;

/// <summary>
/// Implementation of Stripe platform service for direct platform payments.
/// Used for collecting payments from tenants to the platform (e.g., subscription fees).
/// Uses StripeClient for thread-safe, isolated API access.
/// </summary>
public class StripePlatformService : IStripePlatformService
{
    private readonly StripeClient _stripeClient;
    private readonly StripePlatformSettings _settings;

    public StripePlatformService(IOptions<StripeSettings> stripeSettings)
    {
        _settings = stripeSettings.Value.Platform;

        // Create StripeClient with API key (thread-safe, no global config)
        _stripeClient = new StripeClient(_settings.SecretKey);
    }

    public async Task<Session> CreateCheckoutSessionAsync(
        string priceId,
        string successUrl,
        string cancelUrl,
        string? customerEmail,
        Dictionary<string, string>? metadata,
        CancellationToken cancellationToken = default)
    {
        var options = new SessionCreateOptions
        {
            Mode = "subscription",
            LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    Price = priceId,
                    Quantity = 1,
                }
            },
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            CustomerEmail = customerEmail,
            Metadata = metadata,
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = metadata,
            }
        };

        var requestOptions = new RequestOptions
        {
            IdempotencyKey = Guid.NewGuid().ToString(), // Ensure idempotency with Stripe's built-in retry mechanism
        };

        var service = new SessionService(_stripeClient);
        var session = await service.CreateAsync(options, requestOptions, cancellationToken);

        return session;
    }

    public async Task<Session> GetCheckoutSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var service = new SessionService(_stripeClient);
        return await service.GetAsync(sessionId, cancellationToken: cancellationToken);
    }

    public async Task<string> CreateCustomerAsync(
        string email,
        string? name,
        Dictionary<string, string>? metadata,
        CancellationToken cancellationToken = default)
    {
        var options = new CustomerCreateOptions
        {
            Email = email,
            Name = name,
            Metadata = metadata
        };

        var service = new CustomerService(_stripeClient);
        var customer = await service.CreateAsync(options, cancellationToken: cancellationToken);

        return customer.Id;
    }

    public async Task<string> CreateSubscriptionAsync(
        string customerId,
        string priceId,
        Dictionary<string, string>? metadata,
        CancellationToken cancellationToken = default)
    {
        var options = new SubscriptionCreateOptions
        {
            Customer = customerId,
            Items = new List<SubscriptionItemOptions>
            {
                new SubscriptionItemOptions
                {
                    Price = priceId,
                }
            },
            Metadata = metadata,
        };

        var service = new SubscriptionService(_stripeClient);
        var subscription = await service.CreateAsync(options, cancellationToken: cancellationToken);

        return subscription.Id;
    }

    public async Task<Subscription> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        var service = new SubscriptionService(_stripeClient);
        return await service.GetAsync(subscriptionId, cancellationToken: cancellationToken);
    }

    public async Task<List<Subscription>> GetCustomerSubscriptionsAsync(string customerId, CancellationToken cancellationToken = default)
    {
        var options = new SubscriptionListOptions
        {
            Customer = customerId,
        };

        var service = new SubscriptionService(_stripeClient);
        var subscriptions = await service.ListAsync(options, cancellationToken: cancellationToken);

        return subscriptions.Data.ToList();
    }

    public async Task CancelSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        var service = new SubscriptionService(_stripeClient);
        await service.CancelAsync(subscriptionId, cancellationToken: cancellationToken);
    }

    public async Task<string> CreateProductAsync(string name, string? description, CancellationToken cancellationToken = default)
    {
        var options = new ProductCreateOptions
        {
            Name = name,
            Description = description,
        };

        var service = new ProductService(_stripeClient);
        var product = await service.CreateAsync(options, cancellationToken: cancellationToken);

        return product.Id;
    }

    public async Task<string> CreatePriceAsync(
        string productId,
        long amountInCents,
        string currency,
        string? interval,
        CancellationToken cancellationToken = default)
    {
        var options = new PriceCreateOptions
        {
            Product = productId,
            UnitAmount = amountInCents,
            Currency = currency,
        };

        // If interval is provided, it's a recurring price
        if (!string.IsNullOrEmpty(interval))
        {
            options.Recurring = new PriceRecurringOptions
            {
                Interval = interval
            };
        }

        var service = new PriceService(_stripeClient);
        var price = await service.CreateAsync(options, cancellationToken: cancellationToken);

        return price.Id;
    }
}
