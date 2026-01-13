using Microsoft.EntityFrameworkCore;
using Hubletix.Infrastructure.Persistence;
using Hubletix.Infrastructure.Services;
using Hubletix.Core.Models;
using Hubletix.Api.Validators;
using Hubletix.Api.Conventions;
using Hubletix.Api.Middleware;
using Finbuckle.MultiTenant.Extensions;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register authentication services
builder.Services.AddScoped<IClaimsPrincipalFactory, ClaimsPrincipalFactory>();
builder.Services.AddScoped<IAccountService, AccountService>();

// Configure Identity
builder.Services.AddIdentity<Hubletix.Core.Entities.User, Microsoft.AspNetCore.Identity.IdentityRole>(options =>
    {
        // Password settings
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Password.RequiredLength = 8;
        
        // Lockout settings
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
        
        // User settings
        options.User.RequireUniqueEmail = true;
        options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
        options.SignIn.RequireConfirmedEmail = false; // TODO: Set to true when email confirmation is implemented
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddUserValidator<RequireEmailValidator>();

// Configure Cookie Authentication (used by Identity)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = ".Hubletix.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() 
        ? CookieSecurePolicy.SameAsRequest 
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    
    // Set cookie domain to share across subdomains
    var rootDomain = builder.Configuration["AppSettings:RootDomain"];
    if (!string.IsNullOrEmpty(rootDomain))
    {
        // Extract domain without port when local (e.g., "hubletix.home" from "hubletix.home:9000")
        var domain = rootDomain.Split(':')[0];
        // Set with leading dot to share across subdomains
        options.Cookie.Domain = $".{domain}";
    }
    
    options.ExpireTimeSpan = TimeSpan.FromMinutes(15);
    options.SlidingExpiration = true;
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/unauthorized";
});

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PlatformAdmin", policy =>
        policy.RequireClaim("platform_role", "PlatformAdmin"));
    
    options.AddPolicy("TenantAdmin", policy =>
        policy.RequireClaim("tenant_role", "Admin")
              .RequireClaim("tenant_id"));
    
    options.AddPolicy("TenantMember", policy =>
        policy.RequireClaim("tenant_id"));
});

// Add DbContext with factory for TenantStore
builder.Services.AddDbContextFactory<TenantStoreDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("TenantStoreConnection"));
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("AppDbConnection"));
});

// Register Finbuckle.MultiTenant with EFCore Tenant Store
builder.Services.AddMultiTenant<ClubTenantInfo>()
        .WithHostStrategy()
        .WithEFCoreStore<TenantStoreDbContext, ClubTenantInfo>();

// Register onboarding service
builder.Services.AddScoped<ITenantOnboardingService, TenantOnboardingService>();

// Register database initialization service
builder.Services.AddScoped<DatabaseInitializationService>();

// Add in memory caching
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ICacheService, CacheService>();
builder.Services.AddScoped<ITenantConfigService, TenantConfigService>();

// Configure Stripe settings
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));

// Register Stripe services
builder.Services.AddScoped<IStripeConnectService, StripeConnectService>();
builder.Services.AddScoped<IStripePlatformService, StripePlatformService>();

// Configure Razor Pages
var razorPagesBuilder = builder.Services.AddRazorPages(options =>
{
    options.RootDirectory = "/Pages";
    options.Conventions.ConfigureFilter(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
    options.Conventions.Add(new StripFolderPrefixConvention());
    // Add route for root path
    options.Conventions.AddPageRoute("/Platform/Index", "");
    // Add routes for pages that have custom paths that differ from file names
    options.Conventions.AddPageRoute("/Tenant/Events/Detail", "events/{id}");
    options.Conventions.AddPageRoute("/Tenant/Admin/Events/Detail", "admin/events/{id}");
    options.Conventions.AddPageRoute("/Tenant/Admin/Plans/Detail", "admin/plans/{id}");
});

// Enable hot reload of razor pages in development
if (builder.Environment.IsDevelopment())
{
    razorPagesBuilder.AddRazorRuntimeCompilation();
}

razorPagesBuilder.AddRazorOptions(options =>
{
    options.PageViewLocationFormats.Add("/Pages/Tenant/Admin/Shared/{0}.cshtml");
    options.PageViewLocationFormats.Add("/Pages/Tenant/Shared/{0}.cshtml");
});

builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Initialize database on startup (apply migrations and seed data as needed)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    
    try
    {
        var dbInitService = services.GetRequiredService<DatabaseInitializationService>();
        var tenantStoreContextFactory = services.GetRequiredService<IDbContextFactory<TenantStoreDbContext>>();
        var appContext = services.GetRequiredService<AppDbContext>();
        
        using var tenantStoreContext = await tenantStoreContextFactory.CreateDbContextAsync();
        await dbInitService.InitializeDatabaseAsync(tenantStoreContext, appContext);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while initializing the database");
        throw;
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Serve static files from wwwroot, required for Bootstrap CSS/JS
app.UseStaticFiles();

app.UseRouting();

// Hostname-based route enforcement middleware
// Placed after UseRouting to access endpoint metadata for determining page paths
app.UseMiddleware<HostnameRouteMiddleware>();

// Finbuckle MultiTenant middleware - resolves tenant from subdomain or query param
app.UseMultiTenant();

app.UseAuthentication();

// Enrich user claims with tenant-specific information when accessing tenant subdomains
app.UseMiddleware<TenantClaimsMiddleware>();

app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();

