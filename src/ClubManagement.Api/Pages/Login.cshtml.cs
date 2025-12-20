using ClubManagement.Infrastructure.Persistence;
using ClubManagement.Infrastructure.Services;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace ClubManagement.Api.Pages;

public class LoginModel : PublicPageModel
{
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

    public LoginModel(
        IMultiTenantContextAccessor<ClubTenantInfo> multiTenantContextAccessor,
        ITenantConfigService tenantConfigService,
        AppDbContext dbContext
    ) : base(multiTenantContextAccessor, tenantConfigService, dbContext)
    { }

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
    /// Handle login form submission
    /// </summary>
    public async Task<IActionResult> OnPostLoginAsync()
    {
        if (!ModelState.IsValid)
        {
            ErrorMessage = "Please fill in all required fields.";
            return Page();
        }

        // TODO: Implement login functionality with ASP.NET Core Identity
        // This will include:
        // 1. Validate user credentials against the database
        // 2. Check if user belongs to the current tenant
        // 3. Create authentication cookie/JWT token
        // 4. Redirect to member dashboard or return URL
        
        ErrorMessage = "TODO: Login functionality not yet implemented. Identity and authentication system needs to be configured.";
        return Page();
        
        // Future implementation will look something like:
        // var result = await _signInManager.PasswordSignInAsync(Email, Password, RememberMe, lockoutOnFailure: false);
        // if (result.Succeeded)
        // {
        //     SuccessMessage = "Login successful!";
        //     return RedirectToPage("/Member/Dashboard");
        // }
        // else
        // {
        //     ErrorMessage = "Invalid email or password.";
        //     return Page();
        // }
    }

    /// <summary>
    /// Handle signup form submission
    /// </summary>
    public async Task<IActionResult> OnPostSignupAsync()
    {
        if (!ModelState.IsValid)
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

        // TODO: Implement signup functionality with ASP.NET Core Identity
        // This will include:
        // 1. Create new user account in the database
        // 2. Associate user with current tenant
        // 3. Send email verification (optional)
        // 4. Automatically sign in the user or redirect to login
        // 5. Create initial member profile
        
        ErrorMessage = "TODO: Signup functionality not yet implemented. Identity and authentication system needs to be configured.";
        return Page();
        
        // Future implementation will look something like:
        // var user = new User
        // {
        //     UserName = Email,
        //     Email = Email,
        //     FirstName = FirstName,
        //     LastName = LastName,
        //     TenantId = CurrentTenantInfo.Id,
        //     EmailConfirmed = false // Set to true if not requiring email confirmation
        // };
        //
        // var result = await _userManager.CreateAsync(user, Password);
        // if (result.Succeeded)
        // {
        //     await _signInManager.SignInAsync(user, isPersistent: false);
        //     SuccessMessage = "Account created successfully!";
        //     return RedirectToPage("/Member/Dashboard");
        // }
        // else
        // {
        //     ErrorMessage = string.Join(", ", result.Errors.Select(e => e.Description));
        //     return Page();
        // }
    }
}
