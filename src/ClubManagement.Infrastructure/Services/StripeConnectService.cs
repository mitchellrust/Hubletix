using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using ClubManagement.Core.Models;

namespace ClubManagement.Infrastructure.Services;

/// <summary>
/// Implementation of Stripe Connect service for tenant payment processing.
/// </summary>
public class StripeConnectService : IStripeConnectService
{
    private readonly StripeConnectSettings _settings;

    public StripeConnectService(IOptions<StripeSettings> stripeSettings)
    {
        _settings = stripeSettings.Value.Connect;
        
        // Configure Stripe API key
        StripeConfiguration.ApiKey = _settings.PlatformSecretKey;
    }

    public async Task<string> CreateConnectAccountAsync(string tenantId, string email, CancellationToken cancellationToken = default)
    {
        var options = new AccountCreateOptions
        {
            Type = "standard", // Standard account gives tenant full control
            Email = email,
            Metadata = new Dictionary<string, string>
            {
                { "tenant_id", tenantId }
            }
        };

        var service = new AccountService();
        var account = await service.CreateAsync(options, cancellationToken: cancellationToken);
        
        return account.Id;
    }

    public async Task<string> CreateAccountLinkAsync(
        string stripeAccountId,
        string refreshUrl,
        string returnUrl,
        CancellationToken cancellationToken = default)
    {
        var options = new AccountLinkCreateOptions
        {
            Account = stripeAccountId,
            RefreshUrl = refreshUrl,
            ReturnUrl = returnUrl,
            Type = "account_onboarding",
        };

        var service = new AccountLinkService();
        var accountLink = await service.CreateAsync(options, cancellationToken: cancellationToken);
        
        return accountLink.Url;
    }

    public async Task<bool> IsAccountOnboardedAsync(string stripeAccountId, CancellationToken cancellationToken = default)
    {
        var service = new AccountService();
        var account = await service.GetAsync(stripeAccountId, cancellationToken: cancellationToken);
        
        return account.DetailsSubmitted && account.ChargesEnabled && account.PayoutsEnabled;
    }

    public async Task<string> CreateProductAsync(
        string stripeAccountId,
        string name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        var options = new ProductCreateOptions
        {
            Name = name,
            Description = description,
        };

        var requestOptions = new RequestOptions
        {
            StripeAccount = stripeAccountId
        };

        var service = new ProductService();
        var product = await service.CreateAsync(options, requestOptions, cancellationToken);
        
        return product.Id;
    }

    public async Task<string> CreatePriceAsync(
        string stripeAccountId,
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

        var requestOptions = new RequestOptions
        {
            StripeAccount = stripeAccountId
        };

        var service = new PriceService();
        var price = await service.CreateAsync(options, requestOptions, cancellationToken);
        
        return price.Id;
    }

    public async Task<Session> CreateCheckoutSessionAsync(
        string stripeAccountId,
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

        // Calculate application fee if configured
        if (_settings.ApplicationFeePercent > 0)
        {
            // Note: Application fee will be calculated server-side based on the price amount
            // This requires knowing the price amount, which would need to be passed or looked up
        }

        var requestOptions = new RequestOptions
        {
            StripeAccount = stripeAccountId
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options, requestOptions, cancellationToken);
        
        return session;
    }

    public async Task<Session> GetCheckoutSessionAsync(
        string stripeAccountId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var requestOptions = new RequestOptions
        {
            StripeAccount = stripeAccountId
        };

        var service = new SessionService();
        return await service.GetAsync(sessionId, requestOptions: requestOptions, cancellationToken: cancellationToken);
    }

    public async Task<string> CreateCustomerAsync(
        string stripeAccountId,
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

        var requestOptions = new RequestOptions
        {
            StripeAccount = stripeAccountId
        };

        var service = new CustomerService();
        var customer = await service.CreateAsync(options, requestOptions, cancellationToken);
        
        return customer.Id;
    }

    public async Task<string> CreateSubscriptionAsync(
        string stripeAccountId,
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

        var requestOptions = new RequestOptions
        {
            StripeAccount = stripeAccountId
        };

        var service = new SubscriptionService();
        var subscription = await service.CreateAsync(options, requestOptions, cancellationToken);
        
        return subscription.Id;
    }

    public async Task CancelSubscriptionAsync(
        string stripeAccountId,
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var requestOptions = new RequestOptions
        {
            StripeAccount = stripeAccountId
        };

        var service = new SubscriptionService();
        await service.CancelAsync(subscriptionId, requestOptions: requestOptions, cancellationToken: cancellationToken);
    }
}
