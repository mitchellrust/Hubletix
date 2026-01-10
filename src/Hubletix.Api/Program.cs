using Microsoft.EntityFrameworkCore;
using ClubManagement.Infrastructure.Persistence;
using ClubManagement.Infrastructure.Services;
using ClubManagement.Core.Models;
using ClubManagement.Api.Validators;
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
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddMultiTenant<ClubTenantInfo>()
        .WithHostStrategy()
        .WithStaticStrategy("demo") // Default to demo if not found, useful for local development
        .WithEFCoreStore<TenantStoreDbContext, ClubTenantInfo>();
}
else
{
    builder.Services.AddMultiTenant<ClubTenantInfo>()
        .WithHostStrategy()
        .WithEFCoreStore<TenantStoreDbContext, ClubTenantInfo>();
}

// Register onboarding service
builder.Services.AddScoped<ITenantOnboardingService, TenantOnboardingService>();

// Register authentication services
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAccountService, AccountService>();

// Configure Identity
builder.Services.AddIdentity<ClubManagement.Core.Entities.User, Microsoft.AspNetCore.Identity.IdentityRole>(options =>
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

// Configure JWT settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>();
var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
    System.Text.Encoding.UTF8.GetBytes(jwtSettings!.Secret)
);

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = true;
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = key,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
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

// Enable hot reload of razor pages in development
if (builder.Environment.IsDevelopment())
{
    builder.Services
        .AddRazorPages()
        .AddRazorRuntimeCompilation()
        .AddRazorPagesOptions(options =>
        {
            options.RootDirectory = "/Pages";
        })
        .AddRazorOptions(options =>
        {
            options.PageViewLocationFormats.Add("/Pages/Admin/Shared/{0}.cshtml");
        });
}
else
{
    builder.Services.AddRazorPages()
        .AddRazorPagesOptions(options =>
        {
            options.RootDirectory = "/Pages";
        })
        .AddRazorOptions(options =>
        {
            options.PageViewLocationFormats.Add("/Pages/Admin/Shared/{0}.cshtml");
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

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();

