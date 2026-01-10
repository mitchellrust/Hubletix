using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Hubletix.Infrastructure.Services;
using Hubletix.Core.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Hubletix.Infrastructure.Persistence;

namespace Hubletix.Api.Pages.Platform.Signup;

public class SuccessModel : PageModel
{
    private readonly ITenantOnboardingService _onboardingService;
    private readonly AppDbContext _db;
    private readonly ILogger<SuccessModel> _logger;

    public SuccessModel(
        ITenantOnboardingService onboardingService,
        AppDbContext db,
        ILogger<SuccessModel> logger)
    {
        _onboardingService = onboardingService;
        _db = db;
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
                TenantUrl = $"http://{session.Tenant.Subdomain}.localhost";
                PlanName = session.PlatformPlanId.ToUpper(); // TODO: Get from database

                // Auto sign-in the user after successful tenant creation and payment
                if (!string.IsNullOrEmpty(session.UserId))
                {
                    await AutoSignInUserAsync(session.UserId, session.Tenant.Id);
                }
            }

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading success page: {SessionId}", SessionId);
            return RedirectToPage("/Signup/SelectPlan");
        }
    }

    /// <summary>
    /// Automatically signs in the user after tenant creation with owner claims.
    /// </summary>
    private async Task AutoSignInUserAsync(string platformUserId, string tenantId)
    {
        try
        {
            // Get the identity user and platform user
            var platformUser = await _db.PlatformUsers
                .AsNoTracking()
                .Include(pu => pu.IdentityUser)
                .FirstOrDefaultAsync(pu => pu.Id == platformUserId);

            if (platformUser?.IdentityUser == null)
            {
                _logger.LogError("User not found for auto sign-in: PlatformUserId={PlatformUserId}", platformUserId);
                return;
            }

            // Get tenant user to get role and owner status
            var tenantUser = await _db.TenantUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(tu => tu.PlatformUserId == platformUserId
                    && tu.TenantId == tenantId);

            if (tenantUser == null)
            {
                _logger.LogError("TenantUser not found for auto sign-in: PlatformUserId={PlatformUserId}, TenantId={TenantId}",
                    platformUserId, tenantId);
                return;
            }

            // Build claims for sign-in
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, platformUser.IdentityUser.Id),
                new Claim(ClaimTypes.Email, platformUser.IdentityUser.Email ?? ""),
                new Claim("platform_user_id", platformUser.Id),
                new Claim("first_name", platformUser.FirstName),
                new Claim("tenant_id", tenantId),
                new Claim("tenant_role", tenantUser.Role.ToString()),
                new Claim("is_tenant_owner", tenantUser.IsOwner.ToString().ToLower())
            };

            var identity = new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme);
            var principal = new ClaimsPrincipal(identity);

            // Sign in with cookie authentication
            await HttpContext.SignInAsync(
                IdentityConstants.ApplicationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });

            _logger.LogInformation(
                "Auto signed in user: PlatformUserId={PlatformUserId}, TenantId={TenantId}",
                platformUserId, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during auto sign-in for PlatformUserId={PlatformUserId}", platformUserId);
            // Don't throw - let user manually login if auto sign-in fails
        }
    }
}
