using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ClubManagement.Core.Entities;
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

// Register Finbuckle.MultiTenant
builder.Services.AddMultiTenant<ClubTenantInfo>()
    .WithHostStrategy()
    .WithEFCoreStore<TenantStoreDbContext, ClubTenantInfo>();

// Register onboarding service
builder.Services.AddScoped<ITenantOnboardingService, TenantOnboardingService>();

// Add Identity with custom ApplicationUser
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Configure Identity options
builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;
});

builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Apply migrations and seed database
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    
    // Apply pending migrations
    await dbContext.Database.MigrateAsync();
    
    // Seed initial demo tenant if needed
    await SeedDemoTenantAsync(scope.ServiceProvider);
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

/// <summary>
/// Seed a demo tenant if none exists
/// </summary>
async Task SeedDemoTenantAsync(IServiceProvider serviceProvider)
{
    var dbContext = serviceProvider.GetRequiredService<AppDbContext>();
    
    // Check if any tenants exist
    if (await dbContext.Tenants.AnyAsync())
    {
        return; // Database already seeded
    }
    
    var onboardingService = serviceProvider.GetRequiredService<ITenantOnboardingService>();
    
    try
    {
        await onboardingService.OnboardTenantAsync(
            name: "Idaho One Volleyball Club",
            subdomain: "id1",
            adminEmail: "admin@id1.local",
            adminPassword: "id1@12345"
        );
        
        Console.WriteLine("✓ Demo tenant created successfully!");
        Console.WriteLine("  Subdomain: id1");
        Console.WriteLine("  Admin Email: admin@id1.local");
        Console.WriteLine("  Admin Password: id1@12345");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ Error seeding demo tenant: {ex.Message}");
    }
}
