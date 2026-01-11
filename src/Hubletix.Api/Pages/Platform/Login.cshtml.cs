using Finbuckle.MultiTenant.Abstractions;
using Hubletix.Api.Models;
using Hubletix.Infrastructure.Persistence;
using Hubletix.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Hubletix.Api.Pages.Platform;

public class LoginModel : PlatformPageModel
{
    private readonly IAccountService _accountService;
    private readonly IClaimsPrincipalFactory _authService;
    private readonly ILogger<LoginModel> _logger;

    [BindProperty]
    public new string? Email { get; set; }
    
    [BindProperty]
    public string? Password { get; set; }
    
    [BindProperty]
    public bool RememberMe { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }
    
    [TempData]
    public string? SuccessMessage { get; set; }

    public LoginModel(
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor,
        IAccountService accountService,
        IClaimsPrincipalFactory authService,
        ILogger<LoginModel> logger)
        : base(multiTenantContextAccessor)
    {
        _accountService = accountService;
        _authService = authService;
        _logger = logger;
    }

    public IActionResult OnGet()
    {
        // If already authenticated, redirect to tenant selector
        if (IsAuthenticated)
        {
            return RedirectToPage("/Platform/TenantSelector");
        }

        return Page();
    }

    /// <summary>
    /// Handle platform-level login form submission (no tenant context required)
    /// </summary>
    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter both email and password.";
            return Page();
        }

        try
        {
            // Attempt login WITHOUT tenant context (platform-level login)
            var (success, error, identityUser, platformUser) = await _accountService.LoginAsync(
                Email,
                Password,
                tenantId: null  // No tenant context for platform login
            );

            if (!success || identityUser == null || platformUser == null)
            {
                ErrorMessage = error ?? "Login failed. Please check your credentials and try again.";
                return Page();
            }

            // Create claims principal and sign in
            var principal = await _authService.CreateClaimsPrincipalAsync(
                identityUser,
                platformUser.Id,
                tenantId: null  // Platform-level auth, no tenant
            );

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = RememberMe,
                ExpiresUtc = RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddMinutes(15)
            };

            await HttpContext.SignInAsync(
                IdentityConstants.ApplicationScheme,
                principal,
                authProperties
            );

            _logger.LogInformation("User {Email} logged in successfully at platform level", Email);

            // Redirect to tenant selector where user can choose their organization
            return RedirectToPage("/Platform/TenantSelector");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during platform login for {Email}", Email);
            ErrorMessage = "An error occurred during login. Please try again.";
            return Page();
        }
    }
}
