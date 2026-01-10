using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Hubletix.Api.Pages.Platform;

public class LogoutModel : PageModel
{
    private readonly ILogger<LogoutModel> _logger;

    public LogoutModel(ILogger<LogoutModel> logger)
    {
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (User?.Identity?.IsAuthenticated ?? false)
        {
            _logger.LogInformation("User {UserId} logging out", User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }

        TempData["SuccessMessage"] = "You have been logged out successfully.";
        return RedirectToPage("/Platform/Login");
    }
}
