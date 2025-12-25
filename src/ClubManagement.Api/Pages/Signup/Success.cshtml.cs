using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ClubManagement.Infrastructure.Services;
using ClubManagement.Core.Constants;

namespace ClubManagement.Api.Pages.Signup;

public class SuccessModel : PageModel
{
    private readonly ITenantOnboardingService _onboardingService;
    private readonly ILogger<SuccessModel> _logger;

    public SuccessModel(
        ITenantOnboardingService onboardingService,
        ILogger<SuccessModel> logger)
    {
        _onboardingService = onboardingService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string SessionId { get; set; } = string.Empty;

    public bool IsActivated { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public string TenantUrl { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrEmpty(SessionId))
        {
            return RedirectToPage("/Signup/SelectPlan");
        }

        try
        {
            var session = await _onboardingService.GetSignupSessionAsync(SessionId);
            if (session == null)
            {
                _logger.LogWarning("Signup session not found: {SessionId}", SessionId);
                return RedirectToPage("/Signup/SelectPlan");
            }

            // Check if tenant is activated
            IsActivated = session.State == SignupSessionState.Completed;

            if (IsActivated && session.Tenant != null)
            {
                OrganizationName = session.Tenant.Name;
                TenantUrl = $"https://{session.Tenant.Subdomain}.clubmanager.com";
                PlanName = session.PlatformPlanId.ToUpper(); // TODO: Get from database
            }

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading success page: {SessionId}", SessionId);
            return RedirectToPage("/Signup/SelectPlan");
        }
    }
}
