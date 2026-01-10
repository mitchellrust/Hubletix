namespace ClubManagement.Core.Constants;

/// <summary>
/// Tenant status constants
/// </summary>
public static class TenantStatus
{
    /// <summary>
    /// Tenant is pending activation (awaiting successful payment)
    /// </summary>
    public const string PendingActivation = "PendingActivation";
    
    /// <summary>
    /// Tenant is active and fully functional
    /// </summary>
    public const string Active = "Active";
    
    /// <summary>
    /// Tenant is suspended (payment failed or manually suspended)
    /// </summary>
    public const string Suspended = "Suspended";
    
    /// <summary>
    /// Tenant is cancelled (permanently disabled)
    /// </summary>
    public const string Cancelled = "Cancelled";
}
