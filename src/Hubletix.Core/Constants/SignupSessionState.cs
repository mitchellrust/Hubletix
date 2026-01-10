namespace ClubManagement.Core.Constants;

/// <summary>
/// Signup session state constants
/// </summary>
public static class SignupSessionState
{
    /// <summary>
    /// Session started - plan selected
    /// </summary>
    public const string Started = "Started";
    
    /// <summary>
    /// Admin user created
    /// </summary>
    public const string UserCreated = "UserCreated";
    
    /// <summary>
    /// Tenant record created
    /// </summary>
    public const string TenantCreated = "TenantCreated";
    
    /// <summary>
    /// Stripe checkout session started
    /// </summary>
    public const string BillingStarted = "BillingStarted";
    
    /// <summary>
    /// Billing complete - awaiting webhook confirmation
    /// </summary>
    public const string BillingComplete = "BillingComplete";
    
    /// <summary>
    /// Session expired before completion
    /// </summary>
    public const string Expired = "Expired";
    
    /// <summary>
    /// Session completed successfully - tenant activated
    /// </summary>
    public const string Completed = "Completed";
}
