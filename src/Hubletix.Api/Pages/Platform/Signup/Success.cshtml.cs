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
    public string TenantUrl { get; set; } = string.Empty;
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

            if (IsActivated && session.Tenant != null)
            {
                OrganizationName = session.Tenant.Name;

                TenantUrl = $"http://{session.Tenant.Subdomain}.{_configuration["AppSettings:RootDomain"] ?? "hubletix.com"}";
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
