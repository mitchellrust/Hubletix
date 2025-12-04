// ⚠️ REMOVED - This file is no longer used.
// Replaced with Finbuckle.MultiTenant integration.
// Use ClubTenantInfo (ITenantInfo) and TenantStore (IMultiTenantStore) instead.
// 
// The IMultiTenantContext is now injected directly where needed:
// - In ApplicationDbContext for global query filters
// - In TenantOnboardingService for tenant context management
// - In controllers and services for accessing current tenant information

