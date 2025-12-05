# Finbuckle.MultiTenant Quick Reference

## Accessing Current Tenant

### In Controllers
```csharp
public class SomeController : ControllerBase
{
    private readonly IMultiTenantContext<ClubTenantInfo> _multiTenantContext;
    
    public SomeController(IMultiTenantContext<ClubTenantInfo> multiTenantContext)
    {
        _multiTenantContext = multiTenantContext;
    }
    
    public IActionResult SomeAction()
    {
        // Access tenant info
        if (_multiTenantContext?.TenantInfo == null)
            return BadRequest("No tenant");
            
        var tenantId = Guid.Parse(_multiTenantContext.TenantInfo.Id!);
        var tenantName = _multiTenantContext.TenantInfo.Name;
        var tenantIdentifier = _multiTenantContext.TenantInfo.Identifier; // subdomain
        
        return Ok(new { tenantId, tenantName });
    }
}
```

### In Services
```csharp
public class SomeService
{
    private readonly IMultiTenantContext<ClubTenantInfo> _multiTenantContext;
    
    public SomeService(IMultiTenantContext<ClubTenantInfo> multiTenantContext)
    {
        _multiTenantContext = multiTenantContext;
    }
}
```

### In DbContext
```csharp
public class AppDbContext : IdentityDbContext
{
    private readonly IMultiTenantContext<ClubTenantInfo> _multiTenantContext;
    
    public AppDbContext(
        DbContextOptions options,
        IMultiTenantContext<ClubTenantInfo> multiTenantContext)
        : base(options)
    {
        _multiTenantContext = multiTenantContext;
    }
    
    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Query filters automatically filter by current tenant
        builder.Entity<SomeEntity>()
            .HasQueryFilter(e => _multiTenantContext == null || _multiTenantContext.TenantInfo == null ||
                e.TenantId == Guid.Parse(_multiTenantContext.TenantInfo.Id!));
    }
}
```

## Registering Custom Services

In `Program.cs`:
```csharp
// Register a scoped service
builder.Services.AddScoped<IMyService, MyService>();

// Service will automatically receive IMultiTenantContext via DI
public class MyService : IMyService
{
    private readonly IMultiTenantContext<ClubTenantInfo> _multiTenantContext;
    
    public MyService(IMultiTenantContext<ClubTenantInfo> multiTenantContext)
    {
        _multiTenantContext = multiTenantContext;
    }
}
```

## Accessing Tenant Data

### Query with Filters Applied
```csharp
// Global query filters apply automatically
var users = await _dbContext.Users.ToListAsync();
// Only returns users for the current tenant!
```

### Query All Tenants (bypass filter)
```csharp
// Remove filters for cross-tenant queries
var allUsers = await _dbContext.Users
    .IgnoreQueryFilters()
    .ToListAsync();
```

## Testing Local vs Production

### Local Development
Access via query parameter:
```
http://localhost:5000?tenant=demo
http://localhost:5000?tenant=acme
```

Or if hosts file is configured:
```
http://demo.localhost:5000
http://acme.localhost:5000
```

### Production
Access via subdomain:
```
http://demo.mydomain.com
http://acme.mydomain.com
```

## Finbuckle Architecture

```
Request arrives
    ↓
Finbuckle middleware intercepts
    ↓
Extract identifier (subdomain or query param)
    ↓
Call TenantStore.TryGetAsync(identifier)
    ↓
TenantStore queries PostgreSQL for matching tenant
    ↓
ClubTenantInfo is created and set in IMultiTenantContext
    ↓
Request continues (tenant available via DI)
    ↓
DbContext applies global query filters using tenant ID
    ↓
Response sent
    ↓
Context cleaned up (scoped disposal)
```

## Adding New Multi-Tenant Entities

1. Add `TenantId` foreign key:
```csharp
public class MyEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }  // ← Add this
    public Tenant Tenant { get; set; } = null!;
    // ... other properties
}
```

2. Add global query filter in DbContext:
```csharp
builder.Entity<MyEntity>()
    .HasOne(e => e.Tenant)
    .WithMany()  // or existing navigation
    .HasForeignKey(e => e.TenantId)
    .OnDelete(DeleteBehavior.Cascade);

builder.Entity<MyEntity>()
    .HasQueryFilter(e => _multiTenantContext == null || _multiTenantContext.TenantInfo == null || 
        e.TenantId == Guid.Parse(_multiTenantContext.TenantInfo.Id!));
```

3. Create migration:
```bash
dotnet ef migrations add Add_MyEntity -p src/ClubManagement.Infrastructure -s src/ClubManagement.Api
```

## Common Patterns

### Get Current Tenant ID
```csharp
var tenantId = _multiTenantContext?.TenantInfo?.Id != null 
    ? Guid.Parse(_multiTenantContext.TenantInfo.Id) 
    : Guid.Empty;
```

### Check if Tenant Context Exists
```csharp
var hasTenant = _multiTenantContext?.TenantInfo != null;
```

### Store Custom Data in TenantInfo
```csharp
// In ClubTenantInfo, Items can store custom data:
var items = _multiTenantContext.TenantInfo?.Items as Dictionary<string, object>;
if (items?.TryGetValue("CustomKey", out var value) == true)
{
    // Use value
}
```

## Finbuckle.MultiTenant Advantages

✅ **Built-in middleware** - No custom tenant resolution code  
✅ **Store abstraction** - Flexible tenant data sources (DB, config, cache)  
✅ **Query filters** - EF Core integration  
✅ **Dependency injection** - Native ASP.NET Core support  
✅ **Request scoping** - Automatic cleanup  
✅ **Production-ready** - Well-tested library  
✅ **Low boilerplate** - Less custom infrastructure code  
