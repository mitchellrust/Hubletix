# Stripe Integration Guide

## Overview

Your Hubletix application now has a comprehensive Stripe integration with two distinct payment flows:

1. **Stripe Connect** - For tenant payment processing (members pay tenants)
2. **Stripe Platform** - For platform payment processing (tenants pay platform)

This architecture allows you to:
- Enable tenants to collect payments from their members via Stripe Connect
- Collect platform fees from tenants using direct Stripe integration
- Maintain separation of concerns between the two payment flows

## Architecture

### Services Created

#### IStripeConnectService / StripeConnectService
Handles all Stripe Connect operations for tenant accounts:
- Creating Connect accounts for tenants
- Onboarding tenants to Stripe
- Managing products, prices, and subscriptions in tenant accounts
- Creating Checkout sessions for member purchases
- Managing customers and subscriptions

#### IStripePlatformService / StripePlatformService
Handles direct Stripe operations for the platform:
- Creating products and prices for platform offerings
- Creating Checkout sessions for platform purchases
- Managing platform customers and subscriptions
- Processing platform payments

### Configuration Model

**StripeSettings.cs** provides strongly-typed configuration:
```csharp
Stripe:
  Platform:       // For platform payments
    SecretKey
    PublishableKey
    WebhookSecret
  Connect:        // For tenant payments
    PlatformSecretKey
    PlatformPublishableKey
    WebhookSecret
    ApplicationFeePercent
    ClientId
    OnboardingSuccessUrl
    OnboardingFailureUrl
```

## Setup Instructions

### 1. Get Your Stripe Keys

1. Go to [Stripe Dashboard](https://dashboard.stripe.com)
2. Make sure you're in **Test Mode** (toggle in top right)
3. Navigate to **Developers** > **API keys**
4. Copy your **Secret key** (starts with `sk_test_`)
5. Copy your **Publishable key** (starts with `pk_test_`)

### 2. Update Configuration

Update your `appsettings.Development.json`:

```json
{
  "Stripe": {
    "Platform": {
      "SecretKey": "sk_test_YOUR_ACTUAL_SECRET_KEY",
      "PublishableKey": "pk_test_YOUR_ACTUAL_PUBLISHABLE_KEY",
      "WebhookSecret": "whsec_YOUR_WEBHOOK_SECRET"
    },
    "Connect": {
      "PlatformSecretKey": "sk_test_YOUR_ACTUAL_SECRET_KEY",
      "PlatformPublishableKey": "pk_test_YOUR_ACTUAL_PUBLISHABLE_KEY",
      "WebhookSecret": "whsec_YOUR_CONNECT_WEBHOOK_SECRET",
      "ApplicationFeePercent": 0.0,
      "OnboardingSuccessUrl": "http://localhost:9000/admin/stripe/onboarding/success",
      "OnboardingFailureUrl": "http://localhost:9000/admin/stripe/onboarding/failure"
    }
  }
}
```

**Note:** For now, use the same keys for both Platform and Connect. Later, when you set up platform payments, you can use different keys if needed.

### 3. Enable Stripe Connect in Your Dashboard

1. Go to [Stripe Dashboard](https://dashboard.stripe.com)
2. Navigate to **Connect** > **Settings**
3. Under **Connect settings**, enable **Standard accounts**
4. Save your changes

## Usage Examples

### Example 1: Onboard a Tenant to Stripe Connect

```csharp
public class TenantOnboardingController : Controller
{
    private readonly IStripeConnectService _stripeConnect;
    
    public async Task<IActionResult> StartOnboarding(string tenantId, string email)
    {
        // Create Connect account
        var accountId = await _stripeConnect.CreateConnectAccountAsync(tenantId, email);
        
        // Save accountId to tenant record
        // tenant.StripeAccountId = accountId;
        
        // Create onboarding link
        var onboardingUrl = await _stripeConnect.CreateAccountLinkAsync(
            accountId,
            refreshUrl: "https://yourdomain.com/admin/stripe/reauth",
            returnUrl: "https://yourdomain.com/admin/stripe/onboarding/success"
        );
        
        return Redirect(onboardingUrl);
    }
    
    public async Task<IActionResult> CheckOnboardingStatus(string stripeAccountId)
    {
        var isOnboarded = await _stripeConnect.IsAccountOnboardedAsync(stripeAccountId);
        return Ok(new { isOnboarded });
    }
}
```

### Example 2: Create Membership Plan in Stripe

```csharp
public class MembershipPlanService
{
    private readonly IStripeConnectService _stripeConnect;
    private readonly AppDbContext _context;
    
    public async Task<MembershipPlan> CreatePlanWithStripeAsync(
        string tenantId,
        string stripeAccountId,
        MembershipPlan plan)
    {
        // Create product in Stripe
        var productId = await _stripeConnect.CreateProductAsync(
            stripeAccountId,
            plan.Name,
            plan.Description
        );
        
        // Create price in Stripe
        var priceId = await _stripeConnect.CreatePriceAsync(
            stripeAccountId,
            productId,
            plan.PriceInCents,
            "usd",
            plan.BillingInterval.ToLower() // "month" or "year"
        );
        
        // Save Stripe IDs to plan
        plan.StripeProductId = productId;
        plan.StripePriceId = priceId;
        
        _context.MembershipPlans.Add(plan);
        await _context.SaveChangesAsync();
        
        return plan;
    }
}
```

### Example 3: Create Checkout Session for Member Purchase

```csharp
public class CheckoutController : Controller
{
    private readonly IStripeConnectService _stripeConnect;
    private readonly AppDbContext _context;
    
    public async Task<IActionResult> CreateCheckout(string planId)
    {
        var plan = await _context.MembershipPlans
            .Include(p => p.Tenant)
            .FirstOrDefaultAsync(p => p.Id == planId);
            
        if (plan?.Tenant?.StripeAccountId == null)
            return BadRequest("Tenant not connected to Stripe");
        
        var session = await _stripeConnect.CreateCheckoutSessionAsync(
            plan.Tenant.StripeAccountId,
            plan.StripePriceId,
            successUrl: $"https://yourdomain.com/checkout/success?session_id={{CHECKOUT_SESSION_ID}}",
            cancelUrl: "https://yourdomain.com/membershipplans",
            customerEmail: User.Identity.Name,
            metadata: new Dictionary<string, string>
            {
                { "tenant_id", plan.TenantId },
                { "plan_id", planId },
                { "user_id", User.FindFirst("user_id")?.Value }
            }
        );
        
        return Redirect(session.Url);
    }
}
```

### Example 4: Handle Successful Payment

```csharp
public class CheckoutSuccessController : Controller
{
    private readonly IStripeConnectService _stripeConnect;
    private readonly AppDbContext _context;
    
    public async Task<IActionResult> Success(string session_id, string tenantId)
    {
        var tenant = await _context.Tenants.FindAsync(tenantId);
        
        // Retrieve session to get payment details
        var session = await _stripeConnect.GetCheckoutSessionAsync(
            tenant.StripeAccountId,
            session_id
        );
        
        if (session.PaymentStatus == "paid")
        {
            // Create payment record
            var payment = new Payment
            {
                TenantId = tenantId,
                UserId = session.Metadata["user_id"],
                StripePaymentId = session.PaymentIntentId,
                AmountInCents = (int)(session.AmountTotal ?? 0),
                Currency = session.Currency,
                Status = "succeeded",
                PaymentType = "subscription",
                PaymentDate = DateTime.UtcNow
            };
            
            _context.Payments.Add(payment);
            
            // Update user's membership
            var user = await _context.Users.FindAsync(session.Metadata["user_id"]);
            user.MembershipPlanId = session.Metadata["plan_id"];
            
            await _context.SaveChangesAsync();
        }
        
        return View();
    }
}
```

## Testing in Stripe Sandbox

### Test Card Numbers

Use these test card numbers in Stripe Checkout:

- **Success:** `4242 4242 4242 4242`
- **Decline:** `4000 0000 0000 0002`
- **Requires Authentication:** `4000 0025 0000 3155`

Use any future expiry date, any 3-digit CVC, and any billing ZIP code.

### Testing Connect Onboarding

When testing Connect onboarding, Stripe will provide a simplified onboarding flow in test mode. You can use fake data for all fields.

### Viewing Test Data

1. Go to [Stripe Dashboard](https://dashboard.stripe.com)
2. Make sure you're in **Test Mode**
3. Navigate to:
   - **Payments** to see test charges
   - **Connect** > **Accounts** to see connected accounts
   - **Products** to see created products and prices

## Webhooks Setup (Future)

To handle Stripe webhook events:

1. Install Stripe CLI: `brew install stripe/stripe-cli/stripe`
2. Login: `stripe login`
3. Forward webhooks: `stripe listen --forward-to localhost:9000/api/webhooks/stripe`
4. Copy the webhook signing secret and add to `appsettings.Development.json`

Create a webhook controller:
```csharp
[ApiController]
[Route("api/webhooks/stripe")]
public class StripeWebhookController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> HandleWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        
        try
        {
            var stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                webhookSecret
            );
            
            // Handle different event types
            switch (stripeEvent.Type)
            {
                case "checkout.session.completed":
                    // Handle successful checkout
                    break;
                case "customer.subscription.deleted":
                    // Handle subscription cancellation
                    break;
            }
            
            return Ok();
        }
        catch (StripeException)
        {
            return BadRequest();
        }
    }
}
```

## Important Notes

1. **Never commit real API keys to version control**
2. Use environment variables for production keys
3. Always validate webhook signatures
4. Handle errors gracefully with try-catch blocks
5. Test thoroughly in sandbox before going live
6. For production, you'll need to complete Stripe account verification

## Next Steps

1. ✅ Update `appsettings.Development.json` with your Stripe test keys
2. ⬜ Build the application to restore NuGet packages
3. ⬜ Create admin pages for tenant Stripe onboarding
4. ⬜ Create checkout flow for membership purchases
5. ⬜ Set up webhook handling for payment events
6. ⬜ Test the complete payment flow end-to-end
7. ⬜ Add error handling and logging

## Resources

- [Stripe Connect Documentation](https://stripe.com/docs/connect)
- [Stripe Checkout Documentation](https://stripe.com/docs/payments/checkout)
- [Stripe.net Library](https://github.com/stripe/stripe-dotnet)
- [Stripe Testing](https://stripe.com/docs/testing)
