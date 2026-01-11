using Hubletix.Core.Entities;
using Hubletix.Core.Enums;
using Hubletix.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hubletix.Infrastructure.Services;

public interface IAccountService
{
    Task<(bool success, string? error, User? identityUser, PlatformUser? platformUser)> RegisterAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        string? tenantId = null,
        TenantRole tenantRole = TenantRole.Member,
        CancellationToken ct = default);

    Task<(bool success, string? error, User? identityUser, PlatformUser? platformUser)> LoginAsync(
        string email,
        string password,
        string? tenantId = null,
        CancellationToken ct = default);

    Task<bool> AssignTenantRoleAsync(
        string platformUserId,
        string tenantId,
        TenantRole role,
        bool isOwner = false,
        CancellationToken ct = default);
}

public class AccountService : IAccountService
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly AppDbContext _db;
    private readonly ILogger<AccountService> _logger;

    public AccountService(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        AppDbContext db,
        ILogger<AccountService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _logger = logger;
    }

    public async Task<(bool success, string? error, User? identityUser, PlatformUser? platformUser)> RegisterAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        string? tenantId = null,
        TenantRole tenantRole = TenantRole.Member,
        CancellationToken ct = default)
    {
        try
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(email))
            {
                return (false, "Email is required.", null, null);
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return (false, "Password is required.", null, null);
            }

            if (string.IsNullOrWhiteSpace(firstName))
            {
                return (false, "First name is required.", null, null);
            }

            if (string.IsNullOrWhiteSpace(lastName))
            {
                return (false, "Last name is required.", null, null);
            }

            // Check if user already exists
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                return (false, "An account with this email already exists.", null, null);
            }

            // Create Identity user (authentication layer)
            var identityUser = new User
            {
                UserName = email,
                Email = email,
                EmailConfirmed = false // Set to true for now, add email confirmation later
            };

            var result = await _userManager.CreateAsync(identityUser, password);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogWarning("User registration failed for {Email}: {Errors}", email, errors);
                return (false, errors, null, null);
            }

            // Create PlatformUser (domain layer)
            var platformUser = new PlatformUser
            {
                IdentityUserId = identityUser.Id,
                FirstName = firstName,
                LastName = lastName,
                IsActive = true,
            };

            _db.PlatformUsers.Add(platformUser);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("User {UserId} registered successfully with email {Email}", identityUser.Id, email);

            // Assign tenant role if tenantId provided
            if (!string.IsNullOrEmpty(tenantId))
            {
                await AssignTenantRoleAsync(platformUser.Id, tenantId, tenantRole, false, ct);
            }

            return (true, null, identityUser, platformUser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user registration for {Email}", email);
            return (false, "An error occurred during registration. Please try again.", null, null);
        }
    }

    public async Task<(bool success, string? error, User? identityUser, PlatformUser? platformUser)> LoginAsync(
        string email,
        string password,
        string? tenantId = null,
        CancellationToken ct = default)
    {
        try
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(email))
            {
                return (false, "Email is required.", null, null);
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return (false, "Password is required.", null, null);
            }

            var identityUser = await _userManager.FindByEmailAsync(email);
            if (identityUser == null)
            {
                _logger.LogWarning("Login failed: user not found for email {Email}", email);
                return (false, "Invalid email or password.", null, null);
            }

            // Fetch PlatformUser
            var platformUser = await _db.PlatformUsers
                .FirstOrDefaultAsync(pu => pu.IdentityUserId == identityUser.Id, ct);

            if (platformUser == null)
            {
                _logger.LogError("Login failed: PlatformUser not found for identity user {UserId}", identityUser.Id);
                return (false, "Account configuration error. Please contact support.", null, null);
            }

            if (!platformUser.IsActive)
            {
                _logger.LogWarning("Login failed: user {UserId} is inactive", platformUser.Id);
                return (false, "Your account has been deactivated. Please contact support.", null, null);
            }

            // Check if user has access to the specified tenant (REQUIRED)
            if (!string.IsNullOrEmpty(tenantId))
            {
                var hasTenantAccess = await _db.TenantUsers
                    .AnyAsync(tu => tu.PlatformUserId == platformUser.Id 
                        && tu.TenantId == tenantId 
                        && tu.Status == TenantUserStatus.Active, ct);

                if (!hasTenantAccess)
                {
                    _logger.LogWarning("Login failed: user {PlatformUserId} does not have access to tenant {TenantId}",
                        platformUser.Id, tenantId);
                    return (false, "You do not have access to this organization.", null, null);
                }
            }

            var result = await _signInManager.CheckPasswordSignInAsync(identityUser, password, lockoutOnFailure: true);

            if (!result.Succeeded)
            {
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("Login failed: user {UserId} is locked out", identityUser.Id);
                    return (false, "Your account is locked due to too many failed login attempts. Please try again later.", null, null);
                }

                _logger.LogWarning("Login failed: invalid password for user {Email}", email);
                return (false, "Invalid email or password.", null, null);
            }
            
            _logger.LogInformation("User {UserId} logged in successfully", identityUser.Id);
            return (true, null, identityUser, platformUser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for {Email}", email);
            return (false, "An error occurred during login. Please try again.", null, null);
        }
    }

    public async Task<bool> AssignTenantRoleAsync(
        string platformUserId,
        string tenantId,
        TenantRole role,
        bool isOwner = false,
        CancellationToken ct = default)
    {
        // Check if tenant membership already exists
        var existingTenantUser = await _db.TenantUsers
            .FirstOrDefaultAsync(tu => tu.PlatformUserId == platformUserId && tu.TenantId == tenantId, ct);

        if (existingTenantUser != null)
        {
            // Update existing membership
            existingTenantUser.Role = role;
            existingTenantUser.IsOwner = isOwner;
            _logger.LogInformation("Updated tenant role for platform user {PlatformUserId} in tenant {TenantId} to {Role}",
                platformUserId, tenantId, role);
        }
        else
        {
            // Create new tenant membership
            var tenantUser = new TenantUser
            {
                PlatformUserId = platformUserId,
                TenantId = tenantId,
                Role = role,
                Status = TenantUserStatus.Active,
                IsOwner = isOwner,
                CreatedAt = DateTime.UtcNow
            };

            _db.TenantUsers.Add(tenantUser);
            _logger.LogInformation("Assigned tenant role {Role} to platform user {PlatformUserId} in tenant {TenantId}",
                role, platformUserId, tenantId);
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }
}
