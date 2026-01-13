using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Hubletix.Infrastructure.Services;

namespace Hubletix.Api.Pages.Platform.Signup;

public class SetupOrganizationModel : PageModel
{
    private readonly ITenantOnboardingService _onboardingService;
    private readonly ILogger<SetupOrganizationModel> _logger;

    public SetupOrganizationModel(
        ITenantOnboardingService onboardingService,
        ILogger<SetupOrganizationModel> logger)
    {
        _onboardingService = onboardingService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string SessionId { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Organization name is required")]
    [StringLength(100, ErrorMessage = "Organization name cannot exceed 100 characters")]
    public string OrganizationName { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Subdomain is required")]
    [StringLength(50, ErrorMessage = "Subdomain cannot exceed 50 characters")]
    [RegularExpression(@"^[a-z0-9-]+$", ErrorMessage = "Subdomain can only contain lowercase letters, numbers, and hyphens")]
    public string Subdomain { get; set; } = string.Empty;

    [BindProperty]
    [Phone(ErrorMessage = "Invalid phone number")]
    public string? PhoneNumber { get; set; }

    [BindProperty]
    [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters")]
    public string? Address { get; set; }

    [BindProperty]
    [StringLength(100, ErrorMessage = "City cannot exceed 100 characters")]
    public string? City { get; set; }

    [BindProperty]
    [StringLength(50, ErrorMessage = "State cannot exceed 50 characters")]
    public string? State { get; set; }

    [BindProperty]
    [StringLength(20, ErrorMessage = "Postal code cannot exceed 20 characters")]
    public string? PostalCode { get; set; }

    [BindProperty]
    [StringLength(100, ErrorMessage = "Country cannot exceed 100 characters")]
    public string? Country { get; set; }

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

            // Check that user has been created
            if (string.IsNullOrEmpty(session.UserId))
            {
                _logger.LogWarning("Admin user not created yet: {SessionId}", SessionId);
                return RedirectToPage("/Platform/Signup/CreateAccount", new { sessionId = SessionId });
            }

            // Verify user is authenticated
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                _logger.LogWarning("User not authenticated for session: {SessionId}", SessionId);
                var returnUrl = $"/signup/setuporganization?sessionId={SessionId}";
                return RedirectToPage("/Platform/Login", new { returnUrl });
            }

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading signup session: {SessionId}", SessionId);
            return RedirectToPage("/Platform/Signup/SelectPlan");
        }
    }

    public async Task<IActionResult> OnPostCreateOrganizationAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            // Create tenant
            var tenant = await _onboardingService.CreateTenantAsync(
                SessionId,
                OrganizationName,
                Subdomain
            );

            _logger.LogInformation(
                "Created tenant: SessionId={SessionId}, TenantId={TenantId}",
                SessionId,
                tenant.Id
            );

            // Initialize billing (creates Stripe checkout session)
            var checkoutUrl = await _onboardingService.InitializeBillingAsync(
                SessionId,
                $"http://{Request.Host}/Platform/Signup/Success?sessionId={SessionId}",
                $"http://{Request.Host}/Platform/Signup/CreateAccount?sessionId={SessionId}"
            );

            _logger.LogInformation(
                "Billing initialized: SessionId={SessionId}, CheckoutUrl={CheckoutUrl}",
                SessionId,
                checkoutUrl
            );

            // Redirect to Stripe checkout
            return Redirect(checkoutUrl);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to create tenant: {SessionId}", SessionId);
            ModelState.AddModelError("", ex.Message);
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tenant: {SessionId}", SessionId);
            ModelState.AddModelError("", "An error occurred. Please try again.");
            return Page();
        }
    }
}
