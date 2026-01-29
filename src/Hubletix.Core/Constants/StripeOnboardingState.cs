namespace Hubletix.Core.Constants;

/// <summary>
/// Stripe Connect onboarding state constants
/// </summary>
public static class StripeOnboardingState
{
    /// <summary>
    /// Stripe Connect onboarding not started
    /// </summary>
    public const string NotStarted = "NotStarted";

    /// <summary>
    /// Stripe Connect account created, onboarding in progress
    /// </summary>
    public const string AccountCreated = "AccountCreated";

    /// <summary>
    /// User clicked onboarding link and started the process
    /// </summary>
    public const string OnboardingStarted = "OnboardingStarted";

    /// <summary>
    /// Onboarding completed - charges enabled
    /// </summary>
    public const string Completed = "Completed";
}
