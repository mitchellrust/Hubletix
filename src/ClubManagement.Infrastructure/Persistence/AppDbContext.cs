using Microsoft.EntityFrameworkCore;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using Finbuckle.MultiTenant.Abstractions;
using ClubManagement.Core.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace ClubManagement.Infrastructure.Persistence;

/// <summary>
/// Application DbContext with multi-tenant support via Finbuckle.MultiTenant and Identity support.
/// Uses global query filters to enforce data isolation per tenant.
/// </summary>
public class AppDbContext : IdentityDbContext<User>
{
    private readonly IMultiTenantContextAccessor? _multiTenantContextAccessor;

    // Used for dependency injection
    public AppDbContext(
        IMultiTenantContextAccessor multiTenantContextAccessor
    ) : base()
    {
        _multiTenantContextAccessor = multiTenantContextAccessor;
    }

    // Used for dependency injection
    public AppDbContext(
        IMultiTenantContextAccessor multiTenantContextAccessor,
        DbContextOptions<AppDbContext> options
    ) : base(options)
    {
        _multiTenantContextAccessor = multiTenantContextAccessor;
    }

    // Useful for testing, no DI
    public AppDbContext(
        ClubTenantInfo tenantInfo
    ) : base()
    {
        _multiTenantContextAccessor = (IMultiTenantContextAccessor)tenantInfo;
    }

    // Useful for testing, no DI
    public AppDbContext(
        ClubTenantInfo tenantInfo,
        DbContextOptions<AppDbContext> options
    ) : base(options)
    {
        _multiTenantContextAccessor = (IMultiTenantContextAccessor)tenantInfo;
    }

    // DbSets
    public DbSet<Tenant> Tenants { get; set; } = null!;
    public DbSet<Location> Locations { get; set; } = null!;
    public DbSet<MembershipPlan> MembershipPlans { get; set; } = null!;
    public DbSet<Event> Events { get; set; } = null!;
    public DbSet<EventRegistration> EventRegistrations { get; set; } = null!;
    public DbSet<Payment> Payments { get; set; } = null!;
    public DbSet<PlatformPlan> PlatformPlans { get; set; } = null!;
    public DbSet<TenantSubscription> TenantSubscriptions { get; set; } = null!;
    public DbSet<SignupSession> SignupSessions { get; set; } = null!;
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
    public DbSet<TenantUserRole> TenantUserRoles { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // Important: Call base first for Identity tables

        // Get current tenant ID for query filters
        var tenantId = _multiTenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id;

        // Configure Tenant
        builder.Entity<Tenant>()
            .HasKey(t => t.Id);
        builder.Entity<Tenant>()
            .HasIndex(t => t.Subdomain)
            .IsUnique();
        builder.Entity<Tenant>()
            .Property(t => t.ConfigJson)
            .HasColumnType("jsonb");

        // Configure Location with multi-tenant query filter
        builder.Entity<Location>()
            .HasKey(l => l.Id);
        builder.Entity<Location>()
            .HasOne(l => l.Tenant)
            .WithMany(t => t.Locations)
            .HasForeignKey(l => l.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Location>()
            .HasIndex(l => new { l.TenantId, l.IsDefault });
        builder.Entity<Location>()
            .HasQueryFilter(l => l.TenantId == tenantId);

        // Configure User with multi-tenant query filter
        builder.Entity<User>()
            .HasOne(u => u.Tenant)
            .WithMany(t => t.Users)
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false); // Allow null for platform users
        builder.Entity<User>()
            .HasOne(u => u.Location)
            .WithMany(l => l.Users)
            .HasForeignKey(u => u.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();
        builder.Entity<User>()
            .HasQueryFilter(u => u.TenantId == null || u.TenantId == tenantId);

        // Configure MembershipPlan with multi-tenant query filter
        builder.Entity<MembershipPlan>()
            .HasKey(p => p.Id);
        builder.Entity<MembershipPlan>()
            .HasOne(p => p.Tenant)
            .WithMany(t => t.MembershipPlans)
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<MembershipPlan>()
            .HasOne(p => p.Location)
            .WithMany(l => l.MembershipPlans)
            .HasForeignKey(p => p.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<MembershipPlan>()
            .HasIndex(p => new { p.TenantId, p.StripeProductId });
        builder.Entity<MembershipPlan>()
            .HasQueryFilter(p => p.TenantId == tenantId);

        // Configure Event with multi-tenant query filter
        builder.Entity<Event>()
            .HasKey(e => e.Id);
        builder.Entity<Event>()
            .HasOne(e => e.Tenant)
            .WithMany(t => t.Events)
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Event>()
            .HasOne(e => e.Location)
            .WithMany(l => l.Events)
            .HasForeignKey(e => e.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Event>()
            .HasQueryFilter(e => e.TenantId == tenantId);

        // Configure EventRegistration with multi-tenant query filter (via Event)
        builder.Entity<EventRegistration>()
            .HasKey(s => s.Id);
        builder.Entity<EventRegistration>()
            .HasOne(s => s.Event)
            .WithMany(es => es.EventRegistrations)
            .HasForeignKey(s => s.EventId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<EventRegistration>()
            .HasOne(s => s.User)
            .WithMany(u => u.EventRegistrations)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        // EventRegistration filtered through Event relationship

        // Configure Payment with multi-tenant query filter
        builder.Entity<Payment>()
            .HasKey(p => p.Id);
        builder.Entity<Payment>()
            .HasOne(p => p.Tenant)
            .WithMany(t => t.Payments)
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Payment>()
            .HasOne(p => p.User)
            .WithMany(u => u.Payments)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
        builder.Entity<Payment>()
            .HasIndex(p => p.StripePaymentId)
            .IsUnique();
        builder.Entity<Payment>()
            .HasQueryFilter(p => p.TenantId == tenantId);

        // Configure PlatformPlan
        builder.Entity<PlatformPlan>()
            .HasKey(p => p.Id);
        builder.Entity<PlatformPlan>()
            .HasIndex(p => p.StripeProductId);

        // Configure TenantSubscription
        builder.Entity<TenantSubscription>()
            .HasKey(ts => ts.Id);
        builder.Entity<TenantSubscription>()
            .HasOne(ts => ts.Tenant)
            .WithMany(t => t.Subscriptions)
            .HasForeignKey(ts => ts.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<TenantSubscription>()
            .HasOne(ts => ts.PlatformPlan)
            .WithMany(pp => pp.TenantSubscriptions)
            .HasForeignKey(ts => ts.PlatformPlanId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<TenantSubscription>()
            .HasIndex(ts => ts.StripeSubscriptionId)
            .IsUnique();
        builder.Entity<TenantSubscription>()
            .HasIndex(ts => ts.TenantId);

        // Configure SignupSession
        builder.Entity<SignupSession>()
            .HasKey(ss => ss.Id);
        builder.Entity<SignupSession>()
            .HasOne(ss => ss.PlatformPlan)
            .WithMany()
            .HasForeignKey(ss => ss.PlatformPlanId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<SignupSession>()
            .HasOne(ss => ss.User)
            .WithMany()
            .HasForeignKey(ss => ss.UserId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
        builder.Entity<SignupSession>()
            .HasOne(ss => ss.Tenant)
            .WithMany()
            .HasForeignKey(ss => ss.TenantId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
        builder.Entity<SignupSession>()
            .HasIndex(ss => ss.Email);
        builder.Entity<SignupSession>()
            .HasIndex(ss => ss.StripeCheckoutSessionId);

        // Configure RefreshToken
        builder.Entity<RefreshToken>()
            .HasKey(rt => rt.Id);
        builder.Entity<RefreshToken>()
            .HasOne(rt => rt.User)
            .WithMany()
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<RefreshToken>()
            .HasIndex(rt => rt.TokenHash)
            .IsUnique();
        builder.Entity<RefreshToken>()
            .HasIndex(rt => rt.UserId);

        // Configure TenantUserRole
        builder.Entity<TenantUserRole>()
            .HasKey(tur => tur.Id);
        builder.Entity<TenantUserRole>()
            .HasOne(tur => tur.Tenant)
            .WithMany()
            .HasForeignKey(tur => tur.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<TenantUserRole>()
            .HasOne(tur => tur.User)
            .WithMany()
            .HasForeignKey(tur => tur.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<TenantUserRole>()
            .HasIndex(tur => new { tur.TenantId, tur.UserId })
            .IsUnique();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Update UpdatedAt timestamps on modified entities
        foreach (var entity in ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Modified)
            .Select(e => e.Entity))
        {
            var property = entity.GetType().GetProperty("UpdatedAt");
            if (property?.CanWrite == true)
            {
                property.SetValue(entity, DateTime.UtcNow);
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the current tenant ID from the multi-tenant context.
    /// Returns null if no tenant context is available.
    /// </summary>
    public string? GetCurrentTenantId()
    {
        return _multiTenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id;
    }

    /// <summary>
    /// Bypasses tenant query filters for queries where you need cross-tenant access.
    /// Use with caution - typically only for platform admin operations.
    /// Example: context.Users.IgnoreQueryFilters().Where(...)
    /// </summary>
    public IQueryable<T> WithoutTenantFilter<T>() where T : class
    {
        return Set<T>().IgnoreQueryFilters();
    }
}
