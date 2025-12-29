using ClubManagement.Core.Entities;
using Microsoft.AspNetCore.Identity;

namespace ClubManagement.Api.Validators;

/// <summary>
/// Custom validator to ensure users always have an email address.
/// </summary>
public class RequireEmailValidator : IUserValidator<User>
{
    public Task<IdentityResult> ValidateAsync(UserManager<User> manager, User user)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return Task.FromResult(IdentityResult.Failed(
                new IdentityError
                {
                    Code = "EmailRequired",
                    Description = "Email is required."
                }
            ));
        }

        return Task.FromResult(IdentityResult.Success);
    }
}
