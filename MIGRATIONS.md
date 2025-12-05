# EF Core Migrations Guide

## Quick Reference

### Initial Setup ✅ COMPLETED

The initial migrations have been created and are ready to apply:

```
src/ClubManagement.Infrastructure/Migrations/
├── TenantStore/
│   ├── 20251205210628_InitialCreate.cs
│   ├── 20251205210628_InitialCreate.Designer.cs
│   └── TenantStoreDbContextModelSnapshot.cs
└── App/
    ├── 20251205210711_InitialCreate.cs
    ├── 20251205210711_InitialCreate.Designer.cs
    └── AppDbContextModelSnapshot.cs
```

**Automatic Application**: Migrations will be applied automatically when you run the app via `DatabaseInitializationService`.

---

## Common Commands

All commands should be run from the solution root: `/Users/mitchellrust/dev/club-management`

### Creating New Migrations

**For TenantStore changes** (tenant metadata):
```bash
dotnet ef migrations add <MigrationName> \
  --startup-project src/ClubManagement.Api \
  --project src/ClubManagement.Infrastructure \
  --context TenantStoreDbContext \
  --output-dir Migrations/TenantStore
```

**For App changes** (application data):
```bash
dotnet ef migrations add <MigrationName> \
  --startup-project src/ClubManagement.Api \
  --project src/ClubManagement.Infrastructure \
  --context AppDbContext \
  --output-dir Migrations/App
```

**Example**: Adding a new field to Event entity:
```bash
dotnet ef migrations add AddEventCapacity \
  --startup-project src/ClubManagement.Api \
  --project src/ClubManagement.Infrastructure \
  --context AppDbContext \
  --output-dir Migrations/App
```

### Applying Migrations

**Automatic** (Recommended for Development):
- Just run the application
- `DatabaseInitializationService` applies migrations on startup
- No manual intervention needed

**Manual** (For review or production):
```bash
# Apply TenantStore migrations
dotnet ef database update \
  --startup-project src/ClubManagement.Api \
  --project src/ClubManagement.Infrastructure \
  --context TenantStoreDbContext

# Apply App migrations
dotnet ef database update \
  --startup-project src/ClubManagement.Api \
  --project src/ClubManagement.Infrastructure \
  --context AppDbContext
```

### Removing Migrations

**Remove the last migration** (if not yet applied to database):
```bash
# For TenantStore
dotnet ef migrations remove \
  --startup-project src/ClubManagement.Api \
  --project src/ClubManagement.Infrastructure \
  --context TenantStoreDbContext

# For App
dotnet ef migrations remove \
  --startup-project src/ClubManagement.Api \
  --project src/ClubManagement.Infrastructure \
  --context AppDbContext
```

### Generating SQL Scripts

**For production deployments** - review before applying:
```bash
# Generate SQL for all pending migrations
dotnet ef migrations script \
  --startup-project src/ClubManagement.Api \
  --project src/ClubManagement.Infrastructure \
  --context AppDbContext \
  --output migration.sql \
  --idempotent

# Generate SQL for specific migration range
dotnet ef migrations script <FromMigration> <ToMigration> \
  --startup-project src/ClubManagement.Api \
  --project src/ClubManagement.Infrastructure \
  --context AppDbContext \
  --output migration.sql
```

### Viewing Migration History

```bash
# List applied migrations
dotnet ef migrations list \
  --startup-project src/ClubManagement.Api \
  --project src/ClubManagement.Infrastructure \
  --context AppDbContext
```

---

## Development Workflow

### 1. Making Entity Changes

When you need to add/modify entities:

1. **Modify your entity classes** in `ClubManagement.Core/Entities/`
2. **Update DbContext configuration** if needed (relationships, indexes, etc.)
3. **Create migration**:
   ```bash
   dotnet ef migrations add DescriptiveNameOfChange \
     --startup-project src/ClubManagement.Api \
     --project src/ClubManagement.Infrastructure \
     --context AppDbContext \
     --output-dir Migrations/App
   ```
4. **Review the generated migration** in `Migrations/App/`
5. **Run the app** - migration applies automatically
6. **Commit the migration** with your entity changes

### 2. Breaking Changes

For operations that might cause data loss (column removal, renames):

**Two-Step Migration Pattern**:

**Step 1**: Add new column, mark old as nullable
```csharp
// Add migration: AddNewColumn
public string? OldColumn { get; set; }  // Made nullable
public string NewColumn { get; set; } = string.Empty;  // Added
```

**Step 2**: After data migration, remove old column
```csharp
// Add migration: RemoveOldColumn
// Remove OldColumn property entirely
```

### 3. Testing Migrations

**Reset local database**:
```bash
# Drop databases
psql -U postgres -c "DROP DATABASE IF EXISTS clubmanagement_tenantstore;"
psql -U postgres -c "DROP DATABASE IF EXISTS clubmanagement_app;"

# Recreate databases
psql -U postgres -c "CREATE DATABASE clubmanagement_tenantstore;"
psql -U postgres -c "CREATE DATABASE clubmanagement_app;"

# Run app - migrations apply automatically
```

---

## Multi-Tenant Considerations

### Global Query Filters

All multi-tenant entities automatically filter by `TenantId`:
- **Don't** manually add `WHERE TenantId = X` in queries
- **Do** ensure all entities inherit from `BaseEntity`
- **Do** test that tenant isolation works

### Adding New Tenant-Specific Entities

1. **Inherit from BaseEntity** - provides `TenantId` and audit fields
2. **Configure in AppDbContext**:
   ```csharp
   builder.Entity<YourEntity>()
       .HasOne(e => e.Tenant)
       .WithMany(t => t.YourEntities)
       .HasForeignKey(e => e.TenantId)
       .OnDelete(DeleteBehavior.Cascade);
   ```
3. **Create migration** as normal
4. **Global query filter** is applied automatically via `base.OnModelCreating()`

### Shared vs Tenant-Specific Tables

**Current Strategy**: All tables shared with `TenantId` column

If you need tenant-specific tables later:
- Use custom strategy in Finbuckle
- Create separate schemas per tenant
- Requires significant architectural changes

---

## Production Deployment

### Option 1: Automatic (Current Setup)

**Pros**: Simple, zero-downtime for additive changes
**Cons**: Less control, can't review before applying

Your `DatabaseInitializationService` already does this.

### Option 2: Manual Script-Based

**For critical production changes**:

1. **Generate SQL script**:
   ```bash
   dotnet ef migrations script \
     --startup-project src/ClubManagement.Api \
     --project src/ClubManagement.Infrastructure \
     --context AppDbContext \
     --output production-migration.sql \
     --idempotent
   ```

2. **Review the script** - check for blocking operations

3. **Test on staging** with production-like data

4. **Apply during maintenance window**:
   ```bash
   psql -U postgres -d clubmanagement_app -f production-migration.sql
   ```

5. **Deploy application** without auto-migration enabled

### Option 3: Hybrid (Recommended)

**Additive changes** (new tables, columns): Auto-apply
**Breaking changes** (drops, renames): Manual review and script

To disable auto-migration for production:
```csharp
// In Program.cs, add condition:
if (app.Environment.IsDevelopment())
{
    // Only run auto-migration in dev
    await dbInitService.InitializeDatabaseAsync(...);
}
```

---

## Troubleshooting

### "No migrations found"
- Ensure you're in solution root directory
- Check `--project` and `--startup-project` paths
- Verify DbContext has `DbContextFactory` registered

### "Cannot create DbContext"
- Check connection strings in `appsettings.json`
- Ensure PostgreSQL is running
- Verify databases exist

### "A migration has already been applied"
- Use `ef migrations remove` to undo last migration
- Or create a new migration with inverse changes
- Never delete migration files manually

### "Entity type has no key defined"
- Check entity has `Id` property or `[Key]` attribute
- Review `OnModelCreating` for missing configurations

### "Multi-tenant query filter not working"
- Ensure entity inherits from `BaseEntity`
- Check `TenantId` is set correctly
- Verify `UseMultiTenant()` middleware is registered

---

## Best Practices Summary

✅ **DO**:
- Use descriptive migration names
- Review generated migrations before applying
- Commit migrations with entity changes
- Test migrations on realistic data
- Keep migrations small and focused
- Use two-step migrations for breaking changes

❌ **DON'T**:
- Edit migration files manually (except for custom SQL)
- Delete migration files from version control
- Skip testing migrations before production
- Mix manual SQL changes with EF migrations
- Create migrations without building first

---

## Current Status

**Initial Setup**: ✅ Complete
- TenantStore migration created
- App migration created
- DatabaseInitializationService configured
- Auto-migration on startup enabled

**Next Steps**:
1. Run the application to apply migrations
2. Verify demo tenant is created
3. Test tenant isolation with subdomain access
4. Begin feature development

**Access Demo Tenant**:
```
https://demo.localhost:5001
```

---

## Need Help?

**Check migration status**:
```bash
dotnet ef migrations list --context AppDbContext \
  --startup-project src/ClubManagement.Api \
  --project src/ClubManagement.Infrastructure
```

**View DbContext model**:
```bash
dotnet ef dbcontext info --context AppDbContext \
  --startup-project src/ClubManagement.Api \
  --project src/ClubManagement.Infrastructure
```

**Optimize DbContext**:
```bash
dotnet ef dbcontext optimize --context AppDbContext \
  --startup-project src/ClubManagement.Api \
  --project src/ClubManagement.Infrastructure
```
