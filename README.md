# Club Management - Multi-Tenant SaaS Platform

A comprehensive multi-tenant SaaS platform for sports clubs built with .NET 10, ASP.NET Core, and PostgreSQL.

## Project Structure

```
src/
├── Hubletix.Api/           # Web application (Razor Pages + API)
├── Hubletix.Core/          # Domain models and entities
└── Hubletix.Infrastructure/ # EF Core, DbContext, services
```

## Tech Stack

- **.NET 10**
- **ASP.NET Core 10** (Razor Pages)
- **Entity Framework Core 10**
- **PostgreSQL** (with JSONB support)
- **Microsoft.AspNetCore.Identity**
- **Finbuckle.MultiTenant 10.0.0** (multi-tenant SaaS support)

## Multi-Tenancy Architecture

- **Single shared codebase + database**: All tenants share the same database with TenantId column
- **Tenant resolution**: Via **Finbuckle.MultiTenant** library handling subdomain routing (e.g., `clubname.localhost:5000`)
- **Global query filters**: EF Core automatically filters data by TenantId using Finbuckle's tenant context
- **TenantStore**: Custom `IMultiTenantStore` implementation queries tenants from PostgreSQL by subdomain
- **JSONB storage**: Tenant theme and configuration stored as JSONB in PostgreSQL

### Finbuckle.MultiTenant Integration

**Key components:**
- `ClubTenantInfo`: Implements `ITenantInfo` - represents tenant in Finbuckle
- `TenantStore`: Implements `IMultiTenantStore<ClubTenantInfo>` - loads tenants from database
- `IMultiTenantContext<ClubTenantInfo>`: Injected for accessing current tenant info
- `app.UseMultiTenant()`: Middleware that resolves tenant automatically

Finbuckle eliminates the need for manual:
- Tenant context management (AsyncLocal)
- Custom resolution middleware
- Tenant service orchestration

## Database Setup

### Prerequisites

- PostgreSQL 12+ installed and running
- Default credentials: `postgres:postgres`

### Create Database

```bash
psql -U postgres -c "CREATE DATABASE hubletix;"
```

Or create via pgAdmin.

### Run Migrations

Migrations are applied automatically when the application starts.

## Running the Application

### 1. Configure Connection String

Edit `src/Hubletix.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=hubletix;Username=postgres;Password=postgres"
  }
}
```

### 2. Build the Solution

```bash
cd /Users/mitchellrust/dev/Hubletix
dotnet build
```

### 3. Run the Application

```bash
cd src/Hubletix.Api
dotnet run
```

The application will:
- Apply pending migrations automatically
- Seed a demo tenant if the database is empty
- Start on `https://localhost:5001` (or `http://localhost:5000`)

### 4. Access the Application

- **Home**: `http://localhost:5000`
- **API Health Check**: `http://localhost:5000/api/tenants/health`
- **Current Tenant**: `http://localhost:5000/api/tenants/current?tenant=demo`

### Demo Tenant Credentials

When the app first starts, it creates a demo tenant:

- **Subdomain**: `demo`
- **Admin Email**: `admin@demo.local`
- **Admin Password**: `Demo@123456`
- **Tenant Name**: Demo Fitness Club

### Local Development - Tenant Resolution

For localhost development, use query parameters:

```
http://localhost:5000?tenant=demo
```

For production with subdomains:

```
http://demo.mydomain.com
http://acme.mydomain.com
```

## Database Schema Overview

### Core Entities

- **Tenant**: Club/organization record with JSONB config
- **ApplicationUser**: Multi-tenant-aware user (extends ASP.NET Identity)
- **MembershipPlan**: Subscription tiers per tenant
- **MembershipSubscription**: User subscription status (Stripe linked)
- **Event**: Classes/training sessions
- **EventSchedule**: Specific occurrences of events
- **EventSignup**: User registration for event schedules
- **PaymentRecord**: Payment transactions

### Multi-Tenancy Implementation

All tables (except Identity tables) have a `TenantId` column or reference it through relationships. EF Core global query filters automatically enforce data isolation:

```csharp
// Example: Users are filtered by TenantId via Finbuckle context
.HasQueryFilter(u => _multiTenantContext == null || _multiTenantContext.TenantInfo == null || 
    u.TenantId == Guid.Parse(_multiTenantContext.TenantInfo.Id!))
```

The `IMultiTenantContext<ClubTenantInfo>` is managed by Finbuckle:
- Automatically set by middleware based on subdomain/query parameter
- Available for injection in controllers, services, and DbContext
- Request-scoped with proper cleanup between requests

## Key Features

### 1. Finbuckle.MultiTenant Integration
- **TenantStore**: Custom store that queries PostgreSQL for active tenants by subdomain
- **ClubTenantInfo**: Tenant info model that maps to Finbuckle's requirements
- **Middleware**: Automatic tenant resolution from subdomain or query parameter
- **Context injection**: `IMultiTenantContext<ClubTenantInfo>` available throughout the app

### 2. Tenant Onboarding
- Automated tenant creation
- Admin user provisioning
- Default membership plan seeding
- Demo events and schedules creation
- Stripe account placeholder setup

### 3. Identity Integration
- Tenant-scoped roles: Admin, Coach, Member
- Per-tenant user isolation
- Email-based user identification with tenant support

### 4. Data Isolation
- Global query filters enforce TenantId filtering
- JSONB for flexible tenant configuration
- Unique subdomain per tenant

## Project Dependencies

### Hubletix.Core
- Microsoft.AspNetCore.Identity.EntityFrameworkCore
- System.Text.Json

### Hubletix.Infrastructure
- Microsoft.EntityFrameworkCore
- Npgsql.EntityFrameworkCore.PostgreSQL
- Microsoft.AspNetCore.Identity.EntityFrameworkCore
- **Finbuckle.MultiTenant** (7.5.0)
- → References Hubletix.Core

### Hubletix.Api
- Microsoft.EntityFrameworkCore.Design
- Microsoft.AspNetCore.Identity.UI
- **Finbuckle.MultiTenant** (7.5.0)
- → References Core and Infrastructure

## Next Steps (From Your Implementation Plan)

1. ✅ Solution structure scaffolded
2. ✅ Multi-tenant middleware + DbContext with global filters
3. ✅ Tenant + User models with Identity integration
4. ⏳ **Stripe subscription integration + webhooks**
5. ⏳ Admin dashboard pages (membership + event management)
6. ⏳ Member UI (event browsing + signup)

## Troubleshooting

### Database Connection Refused
- Ensure PostgreSQL is running: `brew services list` (macOS)
- Verify credentials in `appsettings.json`
- Check port 5432 is accessible: `psql -U postgres -c "SELECT 1;"`

### No Tenant Found Error
- Ensure demo tenant was seeded (check logs during app startup)
- Use correct tenant subdomain in URL: `?tenant=demo`
- Query database: `SELECT * FROM "Tenants";`

### Migration Issues
- Run migrations explicitly: `dotnet ef database update -p src/Hubletix.Infrastructure -s src/Hubletix.Api`
- View pending migrations: `dotnet ef migrations list -p src/Hubletix.Infrastructure -s src/Hubletix.Api`

## Development Commands

```bash
# Build
dotnet build

# Run tests (when available)
dotnet test

# Add new migration
dotnet ef migrations add MigrationName -p src/Hubletix.Infrastructure -s src/Hubletix.Api

# Update database
dotnet ef database update -p src/Hubletix.Infrastructure -s src/Hubletix.Api

# View database (via psql)
psql -U postgres -d hubletix -c "SELECT * FROM \"Tenants\";"
```

## Architecture Notes

- **Clean layering**: Core (models) → Infrastructure (EF, services, Finbuckle) → Api (UI, controllers)
- **Finbuckle abstraction**: Multi-tenant logic centralized in library, reducing custom code
- **Single responsibility**: Each service handles one concern (onboarding, etc.)
- **Async-first**: All I/O operations use async/await
- **Configuration-driven**: Theme and features in JSONB, not hardcoded
- **Low-maintenance**: Self-service onboarding reduces manual setup, Finbuckle reduces boilerplate

---

Ready for the next phase: Stripe integration, admin dashboard, and member UI!
