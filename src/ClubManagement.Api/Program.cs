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

builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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

