namespace ClubManagement.Core.Constants;

/// <summary>
/// Subscription status constants
/// </summary>
public static class SubscriptionStatus
{
    /// <summary>
    /// Subscription is active and fully functional
    /// </summary>
    public const string Active = "Active";
    
    /// <summary>
    /// Subscription billing is past due
    /// </summary>
    public const string PastDue = "PastDue";
    
    /// <summary>
    /// Subscription has been cancelled, by tenant or platform.
    /// </summary>
    public const string Cancelled = "Cancelled";
    
    /// <summary>
    /// Subscription has not yet been paid.
    /// </summary>
    public const string Unpaid = "Unpaid";

    /// <summary>
    /// Subscription setup is not yet completed.
    /// </summary>
    public const string Incomplete = "Incomplete";
}
