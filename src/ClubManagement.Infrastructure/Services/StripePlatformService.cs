using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using ClubManagement.Core.Models;

namespace ClubManagement.Infrastructure.Services;

/// <summary>
/// Implementation of Stripe platform service for direct platform payments.
/// Used for collecting payments from tenants to the platform (e.g., subscription fees).
/// </summary>
public class StripePlatformService : IStripePlatformService
{
    private readonly StripePlatformSettings _settings;

    public StripePlatformService(IOptions<StripeSettings> stripeSettings)
    {
        _settings = stripeSettings.Value.Platform;
        
        // Configure Stripe API key for platform account
        StripeConfiguration.ApiKey = _settings.SecretKey;
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
            Mode = "subscription", // or "payment" for one-time
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
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options, cancellationToken: cancellationToken);
        
        return session;
    }

    public async Task<Session> GetCheckoutSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var service = new SessionService();
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

        var service = new CustomerService();
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

        var service = new SubscriptionService();
        var subscription = await service.CreateAsync(options, cancellationToken: cancellationToken);
        
        return subscription.Id;
    }

    public async Task CancelSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        var service = new SubscriptionService();
        await service.CancelAsync(subscriptionId, cancellationToken: cancellationToken);
    }

    public async Task<string> CreateProductAsync(string name, string? description, CancellationToken cancellationToken = default)
    {
        var options = new ProductCreateOptions
        {
            Name = name,
            Description = description,
        };

        var service = new ProductService();
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

        var service = new PriceService();
        var price = await service.CreateAsync(options, cancellationToken: cancellationToken);
        
        return price.Id;
    }
}
