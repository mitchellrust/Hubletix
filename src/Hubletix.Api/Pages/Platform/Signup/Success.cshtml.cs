using Microsoft.AspNetCore.Mvc;
using Hubletix.Infrastructure.Services;
using Hubletix.Core.Constants;
using Hubletix.Api.Models;
using Finbuckle.MultiTenant.Abstractions;
using Hubletix.Infrastructure.Persistence;

namespace Hubletix.Api.Pages.Platform.Signup;

public class SuccessModel : PlatformPageModel
{
    private readonly ITenantOnboardingService _onboardingService;
    private readonly ILogger<SuccessModel> _logger;
    private readonly IConfiguration _configuration;

    public SuccessModel(
        ITenantOnboardingService onboardingService,
        ILogger<SuccessModel> logger,
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor,
        IConfiguration configuration)
        : base(multiTenantContextAccessor)
    {
        _onboardingService = onboardingService;
        _logger = logger;
        _configuration = configuration;
    }

    [BindProperty(SupportsGet = true)]
    public string SessionId { get; set; } = string.Empty;

    public bool IsActivated { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public string TenantBaseUrl { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrEmpty(SessionId))
        {
            return RedirectToPage("/Platform/Signup/SelectPlan");
        }

        try
        {
            var session = await _onboardingService.GetSignupSessionAsync(SessionId);
            if (session == null)
            {
                _logger.LogWarning("Signup session not found: {SessionId}", SessionId);
                return RedirectToPage("/Platform/Signup/SelectPlan");
            }

            // Check if tenant is activated
            IsActivated = session.State == SignupSessionState.Completed;

            // If not activated and tenant exists, try to refresh status from Stripe (once per page view)
            if (!IsActivated && session.Tenant != null)
            {
                var cookieName = $"hubletix_signup_refresh_{SessionId}";
                
                // Check if we've already attempted a refresh (within 30 seconds)
                if (!Request.Cookies.ContainsKey(cookieName))
                {
                    _logger.LogInformation("Attempting to refresh platform subscription status from Stripe: {SessionId}", SessionId);
                    
                    // Set cookie to prevent duplicate refresh on auto-reload (30 second TTL)
                    Response.Cookies.Append(cookieName, "1", new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Lax,
                        MaxAge = TimeSpan.FromSeconds(30)
                    });

                    // Attempt to reconcile status from Stripe
                    await _onboardingService.RefreshPlatformSubscriptionAsync(SessionId);

                    // Reload session to get potentially updated state
                    session = await _onboardingService.GetSignupSessionAsync(SessionId);
                    if (session == null)
                    {
                        _logger.LogWarning("Signup session not found after refresh: {SessionId}", SessionId);
                        return RedirectToPage("/Platform/Signup/SelectPlan");
                    }

                    // Re-check activation status
                    IsActivated = session.State == SignupSessionState.Completed;
                }
                else
                {
                    _logger.LogDebug("Skipping Stripe refresh (cookie present): {SessionId}", SessionId);
                }
            }

            if (IsActivated && session.Tenant != null)
            {
                OrganizationName = session.Tenant.Name;

                TenantBaseUrl = $"http://{session.Tenant.Subdomain}.{_configuration["AppSettings:RootDomain"] ?? "hubletix.com"}";
                PlanName = session.PlatformPlan.Name;
            }

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading success page: {SessionId}", SessionId);
            return RedirectToPage("/Platform/Signup/SelectPlan");
        }
    }
}
