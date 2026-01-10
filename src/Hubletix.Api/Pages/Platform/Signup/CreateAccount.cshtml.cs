using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Hubletix.Infrastructure.Services;

namespace Hubletix.Api.Pages.Platform.Signup;

public class CreateAccountModel : PageModel
{
    private readonly ITenantOnboardingService _onboardingService;
    private readonly ILogger<CreateAccountModel> _logger;

    public CreateAccountModel(
        ITenantOnboardingService onboardingService,
        ILogger<CreateAccountModel> logger)
    {
        _onboardingService = onboardingService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string SessionId { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "First name is required")]
    [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
    public string FirstName { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Last name is required")]
    [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
    public string LastName { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$", 
        ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, and one number")]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Please confirm your password")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [BindProperty]
    [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept the terms and conditions")]
    public bool AcceptTerms { get; set; }

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

            // TODO: Get plan name from database
            PlanName = session.PlatformPlanId.ToUpper();

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading signup session: {SessionId}", SessionId);
            return RedirectToPage("/Signup/SelectPlan");
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await OnGetAsync();
            return Page();
        }

        try
        {
            // Create admin user
            var (identityUser, platformUser) = await _onboardingService.CreateAdminUserAsync(
                SessionId,
                Email,
                FirstName,
                LastName,
                Password
            );

            _logger.LogInformation(
                "Created admin user: SessionId={SessionId}, UserId={UserId}",
                SessionId,
                platformUser.Id
            );

            // Redirect to organization setup
            return RedirectToPage("/Signup/SetupOrganization", new { sessionId = SessionId });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to create admin user: {SessionId}", SessionId);
            ModelState.AddModelError("", ex.Message);
            await OnGetAsync();
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating admin user: {SessionId}", SessionId);
            ModelState.AddModelError("", "An error occurred. Please try again.");
            await OnGetAsync();
            return Page();
        }
    }
}
