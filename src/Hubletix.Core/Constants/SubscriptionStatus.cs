namespace ClubManagement.Core.Constants;

/// NOTE: These values should align with Stripe's subscription status values,
/// which is why they are snake case strings.

/// <summary>
/// Subscription status constants
/// </summary>
public static class SubscriptionStatus
{
    /// <summary>
    /// Subscription is active and fully functional
    /// </summary>
    public const string Active = "active";
    
    /// <summary>
    /// Subscription billing is past due
    /// </summary>
    public const string PastDue = "past_due";
    
    /// <summary>
    /// Subscription has been cancelled, by tenant or platform.
    /// </summary>
    public const string Cancelled = "cancelled";
    
    /// <summary>
    /// Subscription has not yet been paid.
    /// </summary>
    public const string Unpaid = "unpaid";

    /// <summary>
    /// Subscription setup is not yet completed.
    /// </summary>
    public const string Incomplete = "incomplete";

    /// <summary>
    /// Initial subscription payment was not completed within 23 hours.
    /// </summary>
    public const string IncompleteExpired = "incomplete_expired";

    /// <summary>
    /// Currently in trial period.
    /// </summary>
    public const string Trialing = "trialing";

    /// <summary>
    /// Currently paused.
    /// </summary>
    public const string Paused = "paused";
}
