using Finbuckle.MultiTenant.Abstractions;
using Hubletix.Api.Models;
using Hubletix.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Hubletix.Api.Pages.Platform;

public class LogoutModel : PlatformPageModel
{
    private readonly ILogger<LogoutModel> _logger;

    public LogoutModel(
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor,
        ILogger<LogoutModel> logger)
        : base(multiTenantContextAccessor)
    {
        _logger = logger;
    }

    public async Task<IActionResult> OnGet()
    {
        return await PerformLogout();
    }

    public async Task<IActionResult> OnPost()
    {
        return await PerformLogout();
    }

    private async Task<IActionResult> PerformLogout()
    {
        if (IsAuthenticated)
        {
            _logger.LogInformation("User {UserId} logging out", PlatformUserId);

            // Sign out the user
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }

        // Redirect to the login page
        return RedirectToPage("/Platform/Login");
    }
}
