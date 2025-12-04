using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Finbuckle.MultiTenant.AspNetCore;
using ClubManagement.Core.Entities;

namespace ClubManagement.Infrastructure.Persistence;

/// <summary>
/// Application DbContext with multi-tenant support via Finbuckle.MultiTenant.
/// Uses global query filters to enforce data isolation per tenant.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private readonly IMultiTenantContext<ClubTenantInfo>? _multiTenantContext;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        IMultiTenantContext<ClubTenantInfo>? multiTenantContext = null)
        : base(options)
    {
        _multiTenantContext = multiTenantContext;
    }

    // DbSets
    public DbSet<Tenant> Tenants { get; set; } = null!;
    public DbSet<MembershipPlan> MembershipPlans { get; set; } = null!;
    public DbSet<MembershipSubscription> MembershipSubscriptions { get; set; } = null!;
    public DbSet<Event> Events { get; set; } = null!;
    public DbSet<EventSchedule> EventSchedules { get; set; } = null!;
    public DbSet<EventSignup> EventSignups { get; set; } = null!;
    public DbSet<PaymentRecord> PaymentRecords { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure Tenant
        builder.Entity<Tenant>()
            .HasKey(t => t.Id);
        builder.Entity<Tenant>()
            .HasIndex(t => t.Subdomain)
            .IsUnique();
        builder.Entity<Tenant>()
            .Property(t => t.ConfigJson)
            .HasColumnType("jsonb");

        // Configure ApplicationUser
        builder.Entity<ApplicationUser>()
            .HasOne(u => u.Tenant)
            .WithMany(t => t.Users)
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Global query filter for ApplicationUser (tenant isolation)
        builder.Entity<ApplicationUser>()
            .HasQueryFilter(u => _multiTenantContext == null || _multiTenantContext.TenantInfo == null || 
                u.TenantId == Guid.Parse(_multiTenantContext.TenantInfo.Id!));

        // Configure MembershipPlan
        builder.Entity<MembershipPlan>()
            .HasKey(p => p.Id);
        builder.Entity<MembershipPlan>()
            .HasOne(p => p.Tenant)
            .WithMany(t => t.MembershipPlans)
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<MembershipPlan>()
            .HasIndex(p => new { p.TenantId, p.StripeProductId });

        // Global query filter for MembershipPlan
        builder.Entity<MembershipPlan>()
            .HasQueryFilter(p => _multiTenantContext == null || _multiTenantContext.TenantInfo == null || 
                p.TenantId == Guid.Parse(_multiTenantContext.TenantInfo.Id!));

        // Configure MembershipSubscription
        builder.Entity<MembershipSubscription>()
            .HasKey(s => s.Id);
        builder.Entity<MembershipSubscription>()
            .HasOne(s => s.User)
            .WithMany(u => u.MembershipSubscriptions)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<MembershipSubscription>()
            .HasOne(s => s.MembershipPlan)
            .WithMany(p => p.Subscriptions)
            .HasForeignKey(s => s.MembershipPlanId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<MembershipSubscription>()
            .HasIndex(s => s.StripeSubscriptionId);

        // Global query filter for MembershipSubscription
        builder.Entity<MembershipSubscription>()
            .HasQueryFilter(s => _multiTenantContext == null || _multiTenantContext.TenantInfo == null || 
                s.User.TenantId == Guid.Parse(_multiTenantContext.TenantInfo.Id!));

        // Configure Event
        builder.Entity<Event>()
            .HasKey(e => e.Id);
        builder.Entity<Event>()
            .HasOne(e => e.Tenant)
            .WithMany(t => t.Events)
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Event>()
            .HasOne(e => e.Coach)
            .WithMany(u => u.CoachingEvents)
            .HasForeignKey(e => e.CoachId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // Global query filter for Event
        builder.Entity<Event>()
            .HasQueryFilter(e => _multiTenantContext == null || _multiTenantContext.TenantInfo == null || 
                e.TenantId == Guid.Parse(_multiTenantContext.TenantInfo.Id!));

        // Configure EventSchedule
        builder.Entity<EventSchedule>()
            .HasKey(s => s.Id);
        builder.Entity<EventSchedule>()
            .HasOne(s => s.Event)
            .WithMany(e => e.Schedules)
            .HasForeignKey(s => s.EventId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<EventSchedule>()
            .HasIndex(s => new { s.EventId, s.DateTimeStart });

        // Global query filter for EventSchedule
        builder.Entity<EventSchedule>()
            .HasQueryFilter(s => _multiTenantContext == null || _multiTenantContext.TenantInfo == null || 
                s.Event.TenantId == Guid.Parse(_multiTenantContext.TenantInfo.Id!));

        // Configure EventSignup
        builder.Entity<EventSignup>()
            .HasKey(s => s.Id);
        builder.Entity<EventSignup>()
            .HasOne(s => s.Schedule)
            .WithMany(es => es.Signups)
            .HasForeignKey(s => s.ScheduleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<EventSignup>()
            .HasOne(s => s.User)
            .WithMany(u => u.EventSignups)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<EventSignup>()
            .HasIndex(s => new { s.ScheduleId, s.UserId })
            .IsUnique();

        // Global query filter for EventSignup
        builder.Entity<EventSignup>()
            .HasQueryFilter(s => _multiTenantContext == null || _multiTenantContext.TenantInfo == null || 
                s.Schedule.Event.TenantId == Guid.Parse(_multiTenantContext.TenantInfo.Id!));

        // Configure PaymentRecord
        builder.Entity<PaymentRecord>()
            .HasKey(p => p.Id);
        builder.Entity<PaymentRecord>()
            .HasOne(p => p.Tenant)
            .WithMany(t => t.PaymentRecords)
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<PaymentRecord>()
            .HasOne(p => p.User)
            .WithMany(u => u.PaymentRecords)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
        builder.Entity<PaymentRecord>()
            .HasIndex(p => p.StripePaymentId)
            .IsUnique();

        // Global query filter for PaymentRecord
        builder.Entity<PaymentRecord>()
            .HasQueryFilter(p => _multiTenantContext == null || _multiTenantContext.TenantInfo == null || 
                p.TenantId == Guid.Parse(_multiTenantContext.TenantInfo.Id!));

        // Configure Identity entities to use Guid
        builder.Entity<IdentityRole<Guid>>().ToTable("AspNetRoles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("AspNetUserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("AspNetUserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("AspNetUserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("AspNetUserTokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("AspNetRoleClaims");
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
}
