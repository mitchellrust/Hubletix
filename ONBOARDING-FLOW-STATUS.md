# Tenant Onboarding Flow - Implementation Status

## Overview
Complete tenant onboarding flow for multi-tenant SaaS platform with Stripe billing integration.

## ✅ COMPLETED

### 1. Core Constants & Entities
- ✅ `TenantStatus.cs` - Status constants (PendingActivation, Active, Suspended, Cancelled)
- ✅ `SignupSessionState.cs` - Session flow states
- ✅ `PlatformPlan.cs` - Platform subscription plans
- ✅ `TenantSubscription.cs` - Tracks tenant subscriptions
- ✅ `SignupSession.cs` - Tracks signup sessions
- ✅ Updated `Tenant.cs` with Status property
- ✅ Updated `User.cs` with Role property

### 2. Database Configuration
- ✅ `AppDbContext.cs` - Added DbSets and EF Core configurations
- ⚠️ **PENDING:** Database migration needs to be created and applied

### 3. Services
- ✅ `TenantOnboardingService.cs` - Complete implementation with all methods:
  - `StartSignupSessionAsync()` - Step 1
  - `CreateAdminUserAsync()` - Step 2 
  - `CreateTenantAsync()` - Step 3
  - `InitializeBillingAsync()` - Step 4
  - `ActivateTenantAsync()` - Step 5 (webhook-driven)
  - `HandleBillingFailureAsync()` - Error handling
  - `GetSignupSessionAsync()` - Session retrieval
  - `ResumeSignupSessionAsync()` - Resume abandoned sessions

### 4. Webhook Controller
- ✅ `StripePlatformWebhookController.cs` - Handles Stripe webhooks
- ⚠️ **NEEDS FIX:** Stripe API property names need correction (see Issues section)

### 5. UI Pages (Signup Flow)
- ✅ `/Signup/SelectPlan.cshtml` + `.cs` - Plan selection page
- ✅ `/Signup/CreateAccount.cshtml` + `.cs` - Account creation page
- ✅ `/Signup/SetupOrganization.cshtml` + `.cs` - Organization setup page
- ✅ `/Signup/Success.cshtml` + `.cs` - Success/processing page

### 6. Admin Dashboard
- ✅ Added "Test Onboarding Flow" button to admin dashboard

## ⚠️ ISSUES TO FIX

### Stripe API Property Names
The `StripePlatformWebhookController.cs` has compilation errors due to incorrect Stripe property names:

**Invoice Properties:**
- `invoice.SubscriptionId` → Should be `invoice.Subscription?.Id`
- `invoice.Subscription` → Needs to be expanded with service call
- `invoice.PaymentIntent` → Should be `invoice.PaymentIntentId` (then fetch if needed)

**Subscription Properties:**
- `subscription.CurrentPeriodStart` → Should be `subscription.CurrentPeriodStart` (DateTime)
- `subscription.CurrentPeriodEnd` → Should be `subscription.CurrentPeriodEnd` (DateTime)

**Fix Required:**
The Invoice object in Stripe doesn't have a full Subscription object by default. You need to:
1. Use `invoice.SubscriptionId` to get the ID
2. Fetch the full Subscription object if needed using `SubscriptionService`
3. Or use expand parameters when constructing the webhook event

## 📋 PENDING TASKS

### 1. Database Migration (HIGH PRIORITY)
```bash
cd /home/mitchellrust/github/mitchellrust/ClubManagement
dotnet ef migrations add TenantOnboardingFlow \
  --project src/ClubManagement.Infrastructure \
  --startup-project src/ClubManagement.Api
dotnet ef database update \
  --project src/ClubManagement.Infrastructure \
  --startup-project src/ClubManagement.Api
```

### 2. Seed Platform Plans
Create initial platform plans in the database with Stripe products/prices:
- Starter Plan ($29/month)
- Professional Plan ($79/month)
- Enterprise Plan ($199/month)

### 3. Fix Stripe Webhook Controller
Update property names and add proper Stripe API calls for expanded objects.

### 4. Default Location Creation
Add logic in `ActivateTenantAsync()` to:
- Create default location for activated tenant
- Associate admin user with default location

### 5. Welcome Email
Implement email service to send welcome email on activation.

### 6. Tenant Suspension Logic
Implement tenant suspension when subscription is deleted/cancelled.

### 7. Testing
- Test complete flow end-to-end
- Test webhook handling with Stripe CLI:
  ```bash
  stripe listen --forward-to localhost:5000/api/webhooks/stripe/platform
  ```
- Test abandoned session resumption
- Test billing failure scenarios

### 8. Update Obsolete `IsActive` Usage
Fix warnings in:
- `DatabaseInitializationService.cs` (lines 140, 174, 199)
- `TenantsController.cs` (line 51)

Replace `tenant.IsActive` with `tenant.Status == TenantStatus.Active`

## 🔄 SIGNUP FLOW

### User Journey
```
1. User visits /Signup/SelectPlan
   ↓
2. Selects a plan → StartSignupSessionAsync()
   ↓
3. Redirects to /Signup/CreateAccount
   ↓
4. Enters account details → CreateAdminUserAsync()
   ↓
5. Redirects to /Signup/SetupOrganization
   ↓
6. Enters org details → CreateTenantAsync() + InitializeBillingAsync()
   ↓
7. Redirects to Stripe Checkout
   ↓
8. User completes payment
   ↓
9. Stripe sends webhook: invoice.paid
   ↓
10. ActivateTenantAsync() → Tenant becomes Active
   ↓
11. Redirects to /Signup/Success
   ↓
12. User can access dashboard
```

### State Machine
```
SignupSession States:
Started → UserCreated → TenantCreated → BillingStarted → BillingComplete → Completed
                                                        ↓
                                                    Expired (after 24h)
```

## 🔐 SECURITY NOTES

1. **Webhook Verification**: Stripe signature verification is implemented
2. **Idempotent Processing**: All webhook handlers safe to call multiple times
3. **Session Expiration**: Signup sessions expire after 24 hours
4. **Password Hashing**: TODO - Will be handled by ASP.NET Core Identity

## 📝 CONFIGURATION REQUIRED

### appsettings.json
```json
{
  "Stripe": {
    "Platform": {
      "SecretKey": "sk_test_...",
      "PublishableKey": "pk_test_...",
      "WebhookSecret": "whsec_..."
    }
  }
}
```

### Environment Variables (Production)
- `Stripe__Platform__SecretKey`
- `Stripe__Platform__WebhookSecret`

## 📚 RELATED DOCUMENTATION

- `LOGIN-PAGE.md` - Login/signup UI implementation
- `STRIPE-SETUP.md` - Stripe Connect setup
- `MIGRATIONS.md` - Database migration guide

## 🎯 NEXT IMMEDIATE STEPS

1. **Fix Stripe webhook controller compilation errors**
2. **Create and apply database migration**
3. **Seed platform plans**
4. **Test the complete flow**

## Last Updated
December 24, 2025
