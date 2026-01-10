using Hubletix.Core.Enums;
using Hubletix.Infrastructure.Persistence;
using Hubletix.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Finbuckle.MultiTenant.Abstractions;

namespace Hubletix.Api.Pages.Platform;

public class LoginModel : PageModel
{
    private readonly IAccountService _accountService;
    private readonly AppDbContext _db;
    private readonly ILogger<LoginModel> _logger;

    [BindProperty]
    public string? Email { get; set; }
    
    [BindProperty]
    public string? Password { get; set; }
    
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
        IAccountService accountService,
        AppDbContext db,
        ILogger<LoginModel> logger
    )
    {
        _accountService = accountService;
        _db = db;
        _logger = logger;
    }

    public IActionResult OnGet()
    {
        // If already authenticated, redirect to home
        if (User?.Identity?.IsAuthenticated ?? false)
        {
            return RedirectToPage("/Platform/Index");
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

        // Allow /Tenant routes (post-login redirect)
        if (returnUrl.StartsWith("/Tenant", StringComparison.OrdinalIgnoreCase))
            return true;

        // Allow other platform routes
        var allowedPrefixes = new[] { "/admin", "/login", "/signup", "/" };
        return allowedPrefixes.Any(prefix => 
            returnUrl.Equals(prefix, StringComparison.OrdinalIgnoreCase) || 
            returnUrl.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    /// Gets the tenant ID from the current request context (from subdomain).
    /// Returns null if on the root domain (platform context).
    /// </summary>
    private string? GetCurrentTenantId()
    {
        var tenantContextAccessor = HttpContext.RequestServices
            .GetRequiredService<IMultiTenantContextAccessor<ClubTenantInfo>>();
        var tenantInfo = tenantContextAccessor.MultiTenantContext?.TenantInfo;
        return tenantInfo?.Id;
    }

    /// <summary>
    /// Builds a claims principal for Cookie authentication, including tenant context if applicable.
    /// </summary>
    private ClaimsPrincipal BuildClaimsPrincipal(
        Core.Entities.User identityUser,
        Core.Entities.PlatformUser platformUser,
        string? tenantId = null,
        Core.Entities.TenantUser? tenantUser = null)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, identityUser.Id),
            new Claim(ClaimTypes.Email, identityUser.Email ?? ""),
            new Claim("platform_user_id", platformUser.Id),
            new Claim("first_name", platformUser.FirstName)
        };

        // Add tenant context if provided
        if (!string.IsNullOrEmpty(tenantId) && tenantUser != null)
        {
            claims.Add(new Claim("tenant_id", tenantId));
            claims.Add(new Claim("tenant_role", tenantUser.Role.ToString()));
            claims.Add(new Claim("is_tenant_owner", tenantUser.IsOwner.ToString().ToLower()));
        }
        // If no tenant context but user has a default tenant, add it
        else if (string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(platformUser.DefaultTenantId))
        {
            claims.Add(new Claim("tenant_id", platformUser.DefaultTenantId));
            claims.Add(new Claim("tenant_role", TenantRole.Member.ToString()));
            claims.Add(new Claim("is_tenant_owner", "false"));
        }

        var identity = new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme);
        return new ClaimsPrincipal(identity);
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
            var currentTenantId = GetCurrentTenantId();

            // Attempt login with optional tenant context
            var (success, error, identityUser, platformUser) = await _accountService.LoginAsync(
                Email,
                Password,
                currentTenantId
            );

            if (!success || identityUser == null || platformUser == null)
            {
                ErrorMessage = error ?? "Login failed. Please try again.";
                return Page();
            }

            // If on a tenant subdomain, get the user's tenant role
            Hubletix.Core.Entities.TenantUser? tenantUser = null;
            if (!string.IsNullOrEmpty(currentTenantId))
            {
                tenantUser = await _db.TenantUsers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(tu => tu.PlatformUserId == platformUser.Id
                        && tu.TenantId == currentTenantId
                        && tu.Status == TenantUserStatus.Active);
            }

            // Build claims principal (includes tenant context if applicable)
            var principal = BuildClaimsPrincipal(identityUser, platformUser, currentTenantId, tenantUser);

            // Sign in with cookie authentication
            await HttpContext.SignInAsync(
                IdentityConstants.ApplicationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });

            _logger.LogInformation("User {Email} logged in successfully. TenantId: {TenantId}",
                Email, currentTenantId ?? "N/A");

            SuccessMessage = "Login successful! Welcome back.";
            
            // Redirect to return URL if valid, otherwise redirect based on context
            if (IsValidReturnUrl(ReturnUrl))
            {
                return LocalRedirect(ReturnUrl!);
            }

            // If on tenant subdomain, redirect to tenant home
            if (!string.IsNullOrEmpty(currentTenantId))
            {
                return RedirectToPage("/Tenant/Index");
            }

            // Redirect to platform home
            return RedirectToPage("/Platform/Index");
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
            // Register new user without tenant scoping
            var (success, error, identityUser, platformUser) = await _accountService.RegisterAsync(
                Email,
                Password,
                FirstName,
                LastName
            );

            if (!success || identityUser == null || platformUser == null)
            {
                ErrorMessage = error ?? "Registration failed. Please try again.";
                return Page();
            }

            // Build claims principal (no tenant context for new signup)
            var principal = BuildClaimsPrincipal(identityUser, platformUser);

            // Sign in with cookie authentication
            await HttpContext.SignInAsync(
                IdentityConstants.ApplicationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });

            _logger.LogInformation("User {Email} registered successfully", Email);

            SuccessMessage = $"Welcome, {FirstName}! Your account has been created.";
            
            // Redirect to signup flow next step
            return RedirectToPage("/Platform/Signup/CreateTenant");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during signup for {Email}", Email);
            ErrorMessage = "An error occurred during registration. Please try again.";
            return Page();
        }
    }
}
