using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hubletix.Api.Pages.Platform;

public class UnauthorizedModel : PageModel
{
    public string? ReturnUrl { get; set; }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
    }
}
