using ClubManagement.Core.Constants;
using ClubManagement.Core.Entities;
using ClubManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClubManagement.Infrastructure.Services;

public interface IAccountService
{
    Task<(bool success, string? error, User? user)> RegisterAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        string? tenantId = null,
        string? tenantRole = null,
        CancellationToken ct = default);

    Task<(bool success, string? error, User? user)> LoginAsync(
        string email,
        string password,
        string? tenantId = null,
        CancellationToken ct = default);

    Task<bool> AssignTenantRoleAsync(
        string userId,
        string tenantId,
        string role,
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

    public async Task<(bool success, string? error, User? user)> RegisterAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        string? tenantId = null,
        string? tenantRole = null,
        CancellationToken ct = default)
    {
        try
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(email))
            {
                return (false, "Email is required.", null);
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return (false, "Password is required.", null);
            }

            if (string.IsNullOrWhiteSpace(firstName))
            {
                return (false, "First name is required.", null);
            }

            if (string.IsNullOrWhiteSpace(lastName))
            {
                return (false, "Last name is required.", null);
            }

            // Check if user already exists
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                return (false, "An account with this email already exists.", null);
            }

            var user = new User
            {
                UserName = email,
                Email = email,
                EmailConfirmed = false, // Set to true for now, add email confirmation later
                FirstName = firstName,
                LastName = lastName,
                TenantId = tenantId,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, password);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogWarning("User registration failed for {Email}: {Errors}", email, errors);
                return (false, errors, null);
            }

            _logger.LogInformation("User {UserId} registered successfully with email {Email}", user.Id, email);

            // Assign tenant role if provided
            if (!string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(tenantRole))
            {
                await AssignTenantRoleAsync(user.Id, tenantId, tenantRole, ct);
            }
            else
            {
                // Assign default tenant role as Member
                if (!string.IsNullOrEmpty(tenantId))
                {
                    await AssignTenantRoleAsync(user.Id, tenantId, UserRoles.Member, ct);
                }
            }

            return (true, null, user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user registration for {Email}", email);
            return (false, "An error occurred during registration. Please try again.", null);
        }
    }

    public async Task<(bool success, string? error, User? user)> LoginAsync(
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
                return (false, "Email is required.", null);
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return (false, "Password is required.", null);
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                _logger.LogWarning("Login failed: user not found for email {Email}", email);
                return (false, "Invalid email or password.", null);
            }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login failed: user {UserId} is inactive", user.Id);
            return (false, "Your account has been deactivated. Please contact support.", null);
        }

        // Check if user has access to the specified tenant
        if (!string.IsNullOrEmpty(tenantId))
        {
            var hasTenantAccess = await _db.Set<TenantUserRole>()
                .AnyAsync(tr => tr.UserId == user.Id && tr.TenantId == tenantId, ct);

            if (!hasTenantAccess && user.TenantId != tenantId)
            {
                _logger.LogWarning("Login failed: user {UserId} does not have access to tenant {TenantId}",
                    user.Id, tenantId);
                return (false, "You do not have access to this organization.", null);
            }
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
            {
                _logger.LogWarning("Login failed: user {UserId} is locked out", user.Id);
                return (false, "Your account is locked due to too many failed login attempts. Please try again later.", null);
            }

            _logger.LogWarning("Login failed: invalid password for user {Email}", email);
            return (false, "Invalid email or password.", null);
        }            _logger.LogInformation("User {UserId} logged in successfully", user.Id);
            return (true, null, user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for {Email}", email);
            return (false, "An error occurred during login. Please try again.", null);
        }
    }

    public async Task<bool> AssignTenantRoleAsync(
        string userId,
        string tenantId,
        string role,
        CancellationToken ct = default)
    {
        // Check if role already exists
        var existingRole = await _db.Set<TenantUserRole>()
            .FirstOrDefaultAsync(tr => tr.UserId == userId && tr.TenantId == tenantId, ct);

        if (existingRole != null)
        {
            // Update existing role
            existingRole.Role = role;
            _logger.LogInformation("Updated tenant role for user {UserId} in tenant {TenantId} to {Role}",
                userId, tenantId, role);
        }
        else
        {
            // Create new role
            var tenantRole = new TenantUserRole
            {
                UserId = userId,
                TenantId = tenantId,
                Role = role,
                CreatedAt = DateTime.UtcNow
            };

            _db.Set<TenantUserRole>().Add(tenantRole);
            _logger.LogInformation("Assigned tenant role {Role} to user {UserId} in tenant {TenantId}",
                role, userId, tenantId);
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }
}
