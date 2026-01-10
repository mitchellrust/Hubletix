using Hubletix.Infrastructure.Persistence;
using Hubletix.Infrastructure.Services;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Hubletix.Api.Pages.Platform;

public class LoginModel : PublicPageModel
{
    private readonly IAccountService _accountService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<LoginModel> _logger;

    [BindProperty]
    public string? Email { get; set; }
    
    [BindProperty]
    public string? Password { get; set; }
    
    [BindProperty]
    public bool RememberMe { get; set; }
    
    [BindProperty]
    public string? FirstName { get; set; }
    
    [BindProperty]
    public string? LastName { get; set; }
    
    [BindProperty]
    public string? ConfirmPassword { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }
    
    [TempData]
    public string? SuccessMessage { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public LoginModel(
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor,
        ITenantConfigService tenantConfigService,
        AppDbContext dbContext,
        IAccountService accountService,
        ITokenService tokenService,
        ILogger<LoginModel> logger
    ) : base(multiTenantContextAccessor, tenantConfigService, dbContext)
    {
        _accountService = accountService;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        // Check if user signup is enabled for this tenant
        if (!TenantConfig.Features.EnableUserSignup)
        {
            ErrorMessage = "Member registration is currently not available.";
            return RedirectToPage("/Index");
        }

        return Page();
    }

    /// <summary>
    /// Validates a return URL to prevent open redirect attacks.
    /// Allows local paths that start with / and are part of valid route patterns.
    /// </summary>
    private bool IsValidReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return false;

        // Must be a local URL (starts with /)
        if (!returnUrl.StartsWith("/"))
            return false;

        // Prevent double-encoding or escape sequences
        if (returnUrl.Contains("//") || returnUrl.Contains("\\"))
            return false;

        // Validate against allowed route patterns
        var allowedPrefixes = new[] { "/events", "/eventdetail", "/membershipplans", "/admin", "/login", "/signup", "/" };
        return allowedPrefixes.Any(prefix => 
            returnUrl.Equals(prefix, StringComparison.OrdinalIgnoreCase) || 
            returnUrl.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    /// Handle login form submission
    /// </summary>
    public async Task<IActionResult> OnPostLoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter both email and password.";
            return Page();
        }

        try
        {
            // Attempt login with tenant context
            var (success, error, identityUser, platformUser) = await _accountService.LoginAsync(
                Email,
                Password,
                CurrentTenantInfo?.Id
            );

            if (!success || identityUser == null || platformUser == null)
            {
                ErrorMessage = error ?? "Login failed. Please try again.";
                return Page();
            }

            // Create JWT tokens
            var (accessToken, refreshToken) = await _tokenService.CreateTokensAsync(
                identityUser,
                platformUser.Id,
                CurrentTenantInfo?.Id
            );

            // Store tokens in HTTP-only cookies for web sessions
            Response.Cookies.Append("access_token", accessToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddMinutes(15)
            });

            Response.Cookies.Append("refresh_token", refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });

            _logger.LogInformation("User {Email} logged in successfully to tenant {TenantId}",
                Email, CurrentTenantInfo?.Id);

            SuccessMessage = "Login successful! Welcome back.";
            
            // Redirect to return URL if valid, otherwise redirect based on context
            if (IsValidReturnUrl(ReturnUrl))
            {
                return LocalRedirect(ReturnUrl!);
            }

            // If on a subdomain (tenant context), redirect to tenant events page
            if (CurrentTenantInfo != null)
            {
                return RedirectToPage("/Events", new { area = "" });
            }

            // Otherwise, redirect to platform home
            return RedirectToPage("/Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for {Email}", Email);
            ErrorMessage = "An error occurred during login. Please try again.";
            return Page();
        }
    }

    /// <summary>
    /// Handle signup form submission
    /// </summary>
    public async Task<IActionResult> OnPostSignupAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password) ||
            string.IsNullOrWhiteSpace(FirstName) || string.IsNullOrWhiteSpace(LastName))
        {
            ErrorMessage = "Please fill in all required fields.";
            return Page();
        }

        // Validate password confirmation
        if (Password != ConfirmPassword)
        {
            ErrorMessage = "Passwords do not match.";
            return Page();
        }

        try
        {
            // Register new user with tenant context
            var (success, error, identityUser, platformUser) = await _accountService.RegisterAsync(
                Email,
                Password,
                FirstName,
                LastName,
                CurrentTenantInfo?.Id,
                Core.Enums.TenantRole.Member
            );

            if (!success || identityUser == null || platformUser == null)
            {
                ErrorMessage = error ?? "Registration failed. Please try again.";
                return Page();
            }

            // Create JWT tokens
            var (accessToken, refreshToken) = await _tokenService.CreateTokensAsync(
                identityUser,
                platformUser.Id,
                CurrentTenantInfo?.Id
            );

            // Store tokens in HTTP-only cookies
            Response.Cookies.Append("access_token", accessToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddMinutes(15)
            });

            Response.Cookies.Append("refresh_token", refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });

            _logger.LogInformation("User {Email} registered successfully for tenant {TenantId}",
                Email, CurrentTenantInfo?.Id);

            SuccessMessage = $"Welcome, {FirstName}! Your account has been created.";
            
            // Redirect to return URL if valid, otherwise redirect based on context
            if (IsValidReturnUrl(ReturnUrl))
            {
                return LocalRedirect(ReturnUrl!);
            }

            // If on a subdomain (tenant context), redirect to tenant events page
            if (CurrentTenantInfo != null)
            {
                return RedirectToPage("/Events", new { area = "" });
            }

            // Otherwise, redirect to platform home
            return RedirectToPage("/Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during signup for {Email}", Email);
            ErrorMessage = "An error occurred during registration. Please try again.";
            return Page();
        }
    }
}
