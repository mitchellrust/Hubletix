using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using Hubletix.Infrastructure.Services;
using Hubletix.Api.Models;
using Finbuckle.MultiTenant.Abstractions;
using Hubletix.Infrastructure.Persistence;

namespace Hubletix.Api.Pages.Platform.Signup;

public class CreateAccountModel : PlatformPageModel
{
    private readonly ITenantOnboardingService _onboardingService;
    private readonly IClaimsPrincipalFactory _authService;
    private readonly ILogger<CreateAccountModel> _logger;

    public CreateAccountModel(
        ITenantOnboardingService onboardingService,
        IClaimsPrincipalFactory authService,
        ILogger<CreateAccountModel> logger,
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor)
        : base(multiTenantContextAccessor)
    {
        _onboardingService = onboardingService;
        _authService = authService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string SessionId { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "First name is required")]
    [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
    public string FormFirstName { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Last name is required")]
    [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
    public string LastName { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    public string FormEmail { get; set; } = string.Empty;

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

            // Check if user has already been created, redirect to setup organization
            if (!string.IsNullOrEmpty(session.UserId))
            {
                _logger.LogWarning("Admin user already created: {SessionId}", SessionId);
                return RedirectToPage("/Platform/Signup/SetupOrganization", new { sessionId = SessionId });
            }

            PlanName = session.PlatformPlan.Name;

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading signup session: {SessionId}", SessionId);
            return RedirectToPage("/Platform/Signup/SelectPlan");
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
                FormEmail,
                FormFirstName,
                LastName,
                Password
            );

            _logger.LogInformation(
                "Created admin user: SessionId={SessionId}, UserId={UserId}",
                SessionId,
                platformUser.Id
            );

            // Create claims principal and sign in
            var principal = await _authService.CreateClaimsPrincipalAsync(
                identityUser,
                platformUser.Id,
                tenantId: null  // Platform-level auth during signup, no tenant context yet
            );

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(60) // Longer session for signup flow
            };

            await HttpContext.SignInAsync(
                IdentityConstants.ApplicationScheme,
                principal,
                authProperties
            );
            
            _logger.LogInformation(
                "Signed in admin user: SessionId={SessionId}, UserId={UserId}",
                SessionId,
                platformUser.Id
            );

            // Redirect to organization setup
            return RedirectToPage("/Platform/Signup/SetupOrganization", new { sessionId = SessionId });
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
