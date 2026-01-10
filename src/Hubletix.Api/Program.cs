using Microsoft.EntityFrameworkCore;
using Hubletix.Infrastructure.Persistence;
using Hubletix.Infrastructure.Services;
using Hubletix.Core.Models;
using Hubletix.Api.Validators;
using Hubletix.Api.Pages;
using Hubletix.Api.Middleware;
using Hubletix.Api.Services;
using Finbuckle.MultiTenant.Extensions;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

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

// Register authentication services
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

// Configure Identity's Application Cookie (used for web authentication)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/Tenant/NoAccess";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PlatformAdmin", policy =>
        policy.RequireClaim("platform_role", "PlatformAdmin"));
    
    options.AddPolicy("TenantAdmin", policy =>
        policy.RequireClaim("tenant_role", "Admin"));
    
    options.AddPolicy("TenantMember", policy =>
        policy.RequireClaim("tenant_id"));
});

// Register database initialization service
builder.Services.AddScoped<DatabaseInitializationService>();

// Add in memory caching
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ICacheService, CacheService>();
builder.Services.AddScoped<ITenantConfigService, TenantConfigService>();

// Register user context service for accessing claims
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContextService, UserContextService>();

// Configure Stripe settings
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));

// Register Stripe services
builder.Services.AddScoped<IStripeConnectService, StripeConnectService>();
builder.Services.AddScoped<IStripePlatformService, StripePlatformService>();

// Enable hot reload of razor pages in development
if (builder.Environment.IsDevelopment())
{
    builder.Services
        .AddRazorPages()
        .AddRazorRuntimeCompilation()
        .AddRazorPagesOptions(options =>
        {
            options.RootDirectory = "/Pages";
            // Allows custom routing convention to strip /Platform and /Tenant from page routes
            options.Conventions.Add(new PageRoutingConvention());

            // Authorization conventions for /Tenant pages
            // All /Tenant pages require authentication
            options.Conventions.AuthorizeFolder("/Tenant");

            // All /Tenant/Admin pages require TenantAdmin policy (tenant_role = Admin claim)
            options.Conventions.AuthorizeFolder("/Tenant/Admin", "TenantAdmin");

            // Apply TenantAuthorizationFilter to /Tenant pages to validate tenant membership
            // This is done via FolderApplicationModelConvention which allows us to add filters by type
            options.Conventions.AddFolderApplicationModelConvention("/Tenant", model =>
            {
                // Add the filter by type, ASP.NET Core will resolve dependencies from DI
                model.Filters.Add(new Microsoft.AspNetCore.Mvc.TypeFilterAttribute(typeof(Hubletix.Api.Filters.TenantAuthorizationFilter)));
            });
        })
        .AddRazorOptions(options =>
        {
            options.PageViewLocationFormats.Add("/Pages/Platform/Shared/{0}.cshtml");
            options.PageViewLocationFormats.Add("/Pages/Tenant/Shared/{0}.cshtml");
            options.PageViewLocationFormats.Add("/Pages/Tenant/Admin/Shared/{0}.cshtml");
        });
}
else
{
    builder.Services.AddRazorPages()
        .AddRazorPagesOptions(options =>
        {
            options.RootDirectory = "/Pages";
            // Allows custom routing convention to strip /Platform and /Tenant from page routes
            options.Conventions.Add(new PageRoutingConvention());

            // Authorization conventions for /Tenant pages
            // All /Tenant pages require authentication
            options.Conventions.AuthorizeFolder("/Tenant");

            // All /Tenant/Admin pages require TenantAdmin policy (tenant_role = Admin claim)
            options.Conventions.AuthorizeFolder("/Tenant/Admin", "TenantAdmin");

            // Apply TenantAuthorizationFilter to /Tenant pages to validate tenant membership
            // This is done via FolderApplicationModelConvention which allows us to add filters by type
            options.Conventions.AddFolderApplicationModelConvention("/Tenant", model =>
            {
                // Add the filter by type, ASP.NET Core will resolve dependencies from DI
                model.Filters.Add(new Microsoft.AspNetCore.Mvc.TypeFilterAttribute(typeof(Hubletix.Api.Filters.TenantAuthorizationFilter)));
            });
        })
        .AddRazorOptions(options =>
        {
            options.PageViewLocationFormats.Add("/Pages/Platform/Shared/{0}.cshtml");
            options.PageViewLocationFormats.Add("/Pages/Tenant/Shared/{0}.cshtml");
            options.PageViewLocationFormats.Add("/Pages/Tenant/Admin/Shared/{0}.cshtml");
        });
}

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

// Finbuckle MultiTenant middleware - resolves tenant from subdomain or query param
app.UseMultiTenant();

// Subdomain-based routing middleware - enforces access control between platform and tenant routes
app.UseSubdomainRouting();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();

