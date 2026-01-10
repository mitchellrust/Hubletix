using Finbuckle.MultiTenant.Abstractions;
using Hubletix.Api.Models;
using Hubletix.Infrastructure.Persistence;
using Hubletix.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Hubletix.Api.Pages.Platform;

public class LoginModel : PlatformPageModel
{
    private readonly IAccountService _accountService;
    private readonly ITokenService _tokenService;
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
        ITokenService tokenService,
        ILogger<LoginModel> logger)
        : base(multiTenantContextAccessor)
    {
        _accountService = accountService;
        _tokenService = tokenService;
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

            // Create JWT tokens without tenant context
            var (accessToken, refreshToken) = await _tokenService.CreateTokensAsync(
                identityUser,
                platformUser.Id,
                tenantId: null  // Platform-level token, no tenant
            );

            // Store tokens in HTTP-only cookies for web sessions
            var cookieExpiry = RememberMe 
                ? DateTimeOffset.UtcNow.AddDays(30) 
                : DateTimeOffset.UtcNow.AddHours(8);

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
                Expires = cookieExpiry
            });

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
