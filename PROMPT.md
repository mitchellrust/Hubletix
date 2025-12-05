# Design Prompt

I am building a **multi-tenant SaaS platform** for sports clubs where clubs can:

- Sign up and get their own tenant instance
- Allow members to create accounts
- Collect dues (Stripe Subscriptions)
- Offer memberships and event signups (e.g., classes, training sessions)

Tech Stack:
- **.NET 10**
- **ASP.NET Core (Razor Pages)**
- **EF Core** for ORM
- **PostgreSQL** database (JSONB fields for configs/themes)
- **Stripe Connect** for club payment accounts

Architecture Requirements:
- **Single shared codebase + shared database + TenantId column** for multi-tenancy
- Middleware to detect tenant via **subdomain routing** (e.g., `clubname.mydomain.com`)
- EF Core **global tenant query filters**
- Store tenant theme + configuration in **JSONB** in the database
- Internal admin tooling for automated tenant creation
- All tenant customization controlled by configuration, not code changes

Database Model — Core Entities:
- Tenant (Name, Subdomain, Theme JSONB, IsActive)
- User (TenantId, roles: Member / Admin / Coach)
- MembershipPlan (Tenant-specific options: price, recurring period)
- MembershipSubscription (Stripe SubscriptionId, UserId, Active flag, etc.)
- Event (TenantId, Name, Description, CoachId if applicable, Capacity)
- EventSchedule (EventId, DateTimeStart, DateTimeEnd)
- EventSignup (ScheduleId, UserId, enforcing unique signup per user/schedule)
- PaymentRecord (StripePaymentId, TenantId, UserId)
- TenantConfig (JSONB for feature flags + settings)

MVP Feature Requirements:
1. Tenant onboarding automation:
    - Create tenant record
    - Create admin user
    - Seed default membership plans + default theme
    - Generate Stripe Connect account automatically
2. Members:
    - Can create accounts tied to a tenant
    - Can subscribe to a membership via Stripe Checkout
3. Admins:
    - Manage memberships and pricing
    - Create/edit events and schedules
4. Members:
    - View available event schedules and register (respecting capacity limits)
    - View/manage own membership status

Implementation Goals:
- Clean layered solution structure: **Api**, **Core**, **Infrastructure**
- Identity authentication + role-based access
- Multi-tenant middleware + tenant context resolution in DbContext
- Mobile-friendly UI design
- Emphasis on low-maintenance and minimal support, enable self-service where possible

For the MVP, I'd like to eventually scaffold out the following:
- Project structure
- EF Core models + DbContext + migrations
- Identity configuration with multi-tenant support
- Stripe subscription integration + webhooks
- Admin dashboard pages for membership + event management
- Member UI for browsing events + signup

Important Notes:
- Ensure onboarding is fast — new tenants should be set up in under 10 minutes
- Do not create separate deployments per tenant — all tenants share a single environment

Please generate the best implementation by scaffolding out the following, following these specifications:
1. Solution structure
2. Multi-tenant middleware + DbContext global filters
3. Tenant + User models + Identity integration

Then stop and wait for my next instruction. I want to get a minimal working app first
with multi-tenant support, seeing the database with an initial demo tenant.
