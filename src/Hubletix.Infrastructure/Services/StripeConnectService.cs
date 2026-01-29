using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using Stripe.V2.Core;
using Hubletix.Core.Models;

namespace Hubletix.Infrastructure.Services;

/// <summary>
/// Implementation of Stripe Connect service for tenant payment processing.
/// Uses StripeClient for thread-safe, isolated API access.
/// </summary>
public class StripeConnectService : IStripeConnectService
{
    private readonly StripeClient _stripeClient;
    private readonly StripeConnectSettings _settings;
    private const string METADATA_TENANT_ID = "tenant_id";
    private const string RESPONSIBILITIES_STRIPE = "stripe";
    private const string DASHBOARD_FULL = "full";
    private const string ACCOUNT_LINK_USE_CASE_ACCOUNT_ONBOARDING = "account_onboarding";
    private const string ACCOUNT_ONBOARDING_COLLECTION_FIELDS = "currently_due";
    private const string PRICE_TAX_BEHAVIOR = "exclusive";
    private const string CHECKOUT_SESSION_MODE_SUBSCRIPTION = "subscription";
    private const string CHECKOUT_SESSION_MODE_PAYMENT = "payment";

    public StripeConnectService(IOptions<StripeSettings> stripeSettings)
    {
        _settings = stripeSettings.Value.Connect;

        // Create StripeClient with API key (thread-safe, no global config)
        _stripeClient = new StripeClient(_settings.PlatformSecretKey);
    }

    // https://docs.stripe.com/api/v2/core/accounts/create?api-version=2025-12-15.clover&rds=1&lang=dotnet
    public async Task<string> CreateConnectAccountAsync(
      string tenantId,
      string name,
      string email,
      TenantConfig tenantConfig,
      CancellationToken cancellationToken = default
    )
    {
        var options = new Stripe.V2.Core.AccountCreateOptions
        {
            ContactEmail = email,
            Dashboard = DASHBOARD_FULL, // Full dashboard access
            DisplayName = name,
            Metadata = new Dictionary<string, string>
            {
                { METADATA_TENANT_ID, tenantId }
            },
            Identity = new AccountCreateIdentityOptions
            {
                Country = tenantConfig.Settings.DefaultCountry,
            },
            Defaults = new AccountCreateDefaultsOptions
            {
                Currency = tenantConfig.Settings.DefaultCurrency,
                Responsibilities = new AccountCreateDefaultsResponsibilitiesOptions
                {
                    FeesCollector = RESPONSIBILITIES_STRIPE, // Stripe will collect fees from the account
                    LossesCollector = RESPONSIBILITIES_STRIPE, // Stripe will be responsible for losses when acount can't pay back negative balances from payments
                }
            },
            Configuration = new AccountCreateConfigurationOptions
            {
                Merchant = new AccountCreateConfigurationMerchantOptions
                {
                    Capabilities = new AccountCreateConfigurationMerchantCapabilitiesOptions
                    {
                        // US Bank Transfers
                        AchDebitPayments = new AccountCreateConfigurationMerchantCapabilitiesAchDebitPaymentsOptions
                        {
                            Requested = true
                        },
                        // Debit and Credit Card Payments
                        CardPayments = new AccountCreateConfigurationMerchantCapabilitiesCardPaymentsOptions
                        {
                            Requested = true
                        },
                    },
                    CardPayments = new AccountCreateConfigurationMerchantCardPaymentsOptions
                    {
                        // Decline payments (or not) matching the specified criteria, regardless if the card issuer approves or not.
                        DeclineOn = new AccountCreateConfigurationMerchantCardPaymentsDeclineOnOptions
                        {
                            // Don't decline on incorrect ZIP or Postal Code if the card issuer approves
                            AvsFailure = false,
                            // Don't decline on incorrect CVC code if the card issuer approves
                            CvcFailure = false,
                        }
                    }
                }
            }
        };

        var accountService = _stripeClient.V2.Core.Accounts;
        var account = await accountService.CreateAsync(options, cancellationToken: cancellationToken);

        return account.Id;
    }

    public async Task<string> UpdateConnectAccountAsync(
      string tenantId,
      string stripeAccountId,
      string name,
      string email,
      TenantConfig tenantConfig,
      CancellationToken cancellationToken = default
    )
    {
        var options = new Stripe.V2.Core.AccountUpdateOptions
        {
            ContactEmail = email,
            Dashboard = DASHBOARD_FULL, // Full dashboard access
            DisplayName = name,
            Metadata = new Dictionary<string, string>
            {
                { METADATA_TENANT_ID, tenantId }
            },
            Identity = new AccountUpdateIdentityOptions
            {
                Country = tenantConfig.Settings.DefaultCountry,
            },
            Defaults = new AccountUpdateDefaultsOptions
            {
                Currency = tenantConfig.Settings.DefaultCurrency,
                Responsibilities = new AccountUpdateDefaultsResponsibilitiesOptions
                {
                    FeesCollector = RESPONSIBILITIES_STRIPE, // Stripe will collect fees from the account
                    LossesCollector = RESPONSIBILITIES_STRIPE, // Stripe will be responsible for losses when acount can't pay back negative balances from payments
                }
            },
            Configuration = new AccountUpdateConfigurationOptions
            {
                Merchant = new AccountUpdateConfigurationMerchantOptions
                {
                    Capabilities = new AccountUpdateConfigurationMerchantCapabilitiesOptions
                    {
                        // US Bank Transfers
                        AchDebitPayments = new AccountUpdateConfigurationMerchantCapabilitiesAchDebitPaymentsOptions
                        {
                            Requested = true
                        },
                        // Debit and Credit Card Payments
                        CardPayments = new AccountUpdateConfigurationMerchantCapabilitiesCardPaymentsOptions
                        {
                            Requested = true
                        },
                    },
                    CardPayments = new AccountUpdateConfigurationMerchantCardPaymentsOptions
                    {
                        // Decline payments (or not) matching the specified criteria, regardless if the card issuer approves or not.
                        DeclineOn = new AccountUpdateConfigurationMerchantCardPaymentsDeclineOnOptions
                        {
                            // Don't decline on incorrect ZIP or Postal Code if the card issuer approves
                            AvsFailure = false,
                            // Don't decline on incorrect CVC code if the card issuer approves
                            CvcFailure = false,
                        }
                    }
                }
            }
        };

        var accountService = _stripeClient.V2.Core.Accounts;
        var account = await accountService.UpdateAsync(stripeAccountId, options, cancellationToken: cancellationToken);

        return account.Id;
    }

    public async Task<Stripe.Account?> GetConnectAccountAsync(
        string stripeAccountId,
        CancellationToken cancellationToken = default
    )
    {
        var service = new Stripe.AccountService(_stripeClient);
        var account = await service.GetAsync(stripeAccountId, cancellationToken: cancellationToken);

        return account;
    }

    public async Task<string> CreateAccountLinkAsync(
        string stripeAccountId,
        string refreshUrl,
        string returnUrl,
        CancellationToken cancellationToken = default)
    {
        var options = new Stripe.AccountLinkCreateOptions
        {
            Account = stripeAccountId,
            RefreshUrl = refreshUrl,
            ReturnUrl = returnUrl,
            Type = ACCOUNT_LINK_USE_CASE_ACCOUNT_ONBOARDING,
            CollectionOptions = new AccountLinkCollectionOptionsOptions
            {
                // Only collect the currently due information on account creation
                Fields = ACCOUNT_ONBOARDING_COLLECTION_FIELDS,
            },
        };

        var accountLinkService = new Stripe.AccountLinkService(_stripeClient);
        var accountLink = await accountLinkService.CreateAsync(options, cancellationToken: cancellationToken);

        return accountLink.Url;
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
            StripeAccount = stripeAccountId,
            IdempotencyKey = Guid.NewGuid().ToString(), // Ensure idempotency with Stripe's built-in retry mechanism
        };

        var service = new ProductService(_stripeClient);
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
            TaxBehavior = PRICE_TAX_BEHAVIOR, // Prices in the app do not include
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
            StripeAccount = stripeAccountId,
            IdempotencyKey = Guid.NewGuid().ToString(), // Ensure idempotency with Stripe's built-in retry mechanism
        };

        var service = new PriceService(_stripeClient);
        var price = await service.CreateAsync(options, requestOptions, cancellationToken);

        return price.Id;
    }

    public async Task<Session> CreateCheckoutSessionAsync(
        string stripeAccountId,
        string priceId,
        long applicationFeeAmountInCents,
        bool isRecurring,
        string successUrl,
        string cancelUrl,
        string? stripeCustomerId,
        string? customerEmail,
        Dictionary<string, string>? metadata,
        CancellationToken cancellationToken = default)
    {
        var options = new SessionCreateOptions
        {
            Mode = isRecurring ? CHECKOUT_SESSION_MODE_SUBSCRIPTION : CHECKOUT_SESSION_MODE_PAYMENT,
            AutomaticTax = new SessionAutomaticTaxOptions
            {
                // Calculate tax automatically based on customer's location
                Enabled = true,
            },
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
            Customer = stripeCustomerId,
            CustomerEmail = customerEmail,
            Metadata = metadata,
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                // Set application fee for the platform, calculated as a percent on the price amount ahead of time
                ApplicationFeeAmount = applicationFeeAmountInCents
            },
        };

        var requestOptions = new RequestOptions
        {
            StripeAccount = stripeAccountId,
            IdempotencyKey = Guid.NewGuid().ToString(), // Ensure idempotency with Stripe's built-in retry mechanism
        };

        var service = new SessionService(_stripeClient);
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

        var service = new SessionService(_stripeClient);
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
            StripeAccount = stripeAccountId,
        };

        var service = new CustomerService(_stripeClient);
        var customer = await service.CreateAsync(options, requestOptions, cancellationToken);

        return customer.Id;
    }

    // TODO: Not sure if this is needed if we do checkout sessions instead, leaving for now.
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

        var service = new SubscriptionService(_stripeClient);
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

        var service = new SubscriptionService(_stripeClient);
        await service.CancelAsync(subscriptionId, requestOptions: requestOptions, cancellationToken: cancellationToken);
    }
}
