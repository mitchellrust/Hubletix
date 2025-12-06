using Microsoft.EntityFrameworkCore;
using ClubManagement.Infrastructure.Persistence;
using ClubManagement.Infrastructure.Services;
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

// Register database initialization service
builder.Services.AddScoped<DatabaseInitializationService>();

// Add in memory caching (for tenant info caching)
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ITenantConfigCacheService, TenantConfigCacheService>();

// Enable hot reload of razor pages in development
if (builder.Environment.IsDevelopment())
{
    builder.Services
        .AddRazorPages()
        .AddRazorRuntimeCompilation();
}
else
{
    builder.Services.AddRazorPages();
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

// Finbuckle MultiTenant middleware - resolves tenant from subdomain or query param
app.UseMultiTenant();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();

