namespace Hubletix.Core.Constants;

/// <summary>
/// Stripe Connect onboarding state constants
/// </summary>
public static class StripeAccountRequirementsStatus
{
    /// <summary>
    /// Stripe Connect onboarding not started
    /// </summary>
    public const string PendingVerification = "PendingVerification";

    /// <summary>
    /// Stripe Connect account created, onboarding in progress
    /// </summary>
    public const string PastDue = "PastDue";

    /// <summary>
    /// User clicked onboarding link and started the process
    /// </summary>
    public const string CurrentlyDue = "CurrentlyDue";

    /// <summary>
    /// There are some requirements due in the future
    /// </summary>
    public const string EventuallyDue = "EventuallyDue";

    /// <summary>
    /// No requirements outstanding
    /// </summary>
    public const string None = "None";
}
