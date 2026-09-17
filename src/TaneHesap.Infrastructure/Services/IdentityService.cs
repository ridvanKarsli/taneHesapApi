using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Enums;
using TaneHesap.Infrastructure.Identity;

namespace TaneHesap.Infrastructure.Services;

/// <summary>
/// IIdentityService'in ASP.NET Core Identity (UserManager) üzerinden implementasyonu.
/// bkz. Proje Raporu bölüm 2, 3.7.
/// </summary>
public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITotpService _totpService;

    public IdentityService(UserManager<ApplicationUser> userManager, ITotpService totpService)
    {
        _userManager = userManager;
        _totpService = totpService;
    }

    public async Task<IdentityOperationResult> CreateAdminOrSuperAdminAsync(
        string username, string password, string fullName, UserRole role, Guid? businessId)
    {
        if (role == UserRole.Employee)
        {
            return IdentityOperationResult.Failure("Bu metot sadece SUPER_ADMIN/ADMIN oluşturmak içindir.");
        }

        var user = new ApplicationUser
        {
            UserName = username,
            FullName = fullName,
            Role = role,
            BusinessId = businessId,
            IsActive = true,
            TotpSecret = _totpService.GenerateSecret(),
            TotpEnabled = false
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return IdentityOperationResult.Failure(result.Errors.Select(e => e.Description).ToArray());
        }

        await _userManager.AddToRoleAsync(user, role.ToString());
        return IdentityOperationResult.Success(user.Id);
    }

    public async Task<IdentityOperationResult> CreateEmployeeAsync(Guid businessId, string username, string password, string fullName)
    {
        var user = new ApplicationUser
        {
            UserName = username,
            FullName = fullName,
            Role = UserRole.Employee,
            BusinessId = businessId,
            IsActive = true
            // EMPLOYEE için TOTP yok (bkz. Proje Raporu bölüm 2).
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return IdentityOperationResult.Failure(result.Errors.Select(e => e.Description).ToArray());
        }

        await _userManager.AddToRoleAsync(user, UserRole.Employee.ToString());
        return IdentityOperationResult.Success(user.Id);
    }

    public async Task<bool> UpdateEmployeeAsync(Guid userId, Guid businessId, string? fullName, string? username, string? newPassword)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == userId && u.BusinessId == businessId && u.Role == UserRole.Employee);
        if (user is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            user.FullName = fullName;
        }

        if (!string.IsNullOrWhiteSpace(username) && username != user.UserName)
        {
            await _userManager.SetUserNameAsync(user, username);
        }

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            await _userManager.ResetPasswordAsync(user, token, newPassword);
        }

        await _userManager.UpdateAsync(user);
        return true;
    }

    public async Task<bool> SetActiveAsync(Guid userId, bool isActive)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return false;
        }

        user.IsActive = isActive;
        await _userManager.UpdateAsync(user);
        return true;
    }

    public async Task<ApplicationUserInfo?> ValidatePasswordAsync(string username, string password)
    {
        var user = await _userManager.FindByNameAsync(username);
        if (user is null)
        {
            return null;
        }

        var isValid = await _userManager.CheckPasswordAsync(user, password);
        return isValid ? ToInfo(user) : null;
    }

    public async Task<ApplicationUserInfo?> GetByIdAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user is null ? null : ToInfo(user);
    }

    public async Task<List<ApplicationUserInfo>> GetEmployeesByBusinessAsync(Guid businessId)
    {
        var users = await _userManager.Users
            .Where(u => u.BusinessId == businessId && u.Role == UserRole.Employee)
            .ToListAsync();

        return users.Select(ToInfo).ToList();
    }

    public async Task<List<ApplicationUserInfo>> GetAdminsByBusinessAsync(Guid businessId)
    {
        var users = await _userManager.Users
            .Where(u => u.BusinessId == businessId && u.Role == UserRole.Admin && u.IsActive)
            .ToListAsync();

        return users.Select(ToInfo).ToList();
    }

    public Task<bool> AnySuperAdminExistsAsync()
        => _userManager.Users.AnyAsync(u => u.Role == UserRole.SuperAdmin);

    public async Task<string> GetOrCreateTotpSecretAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("Kullanıcı bulunamadı.");

        if (string.IsNullOrEmpty(user.TotpSecret))
        {
            user.TotpSecret = _totpService.GenerateSecret();
            await _userManager.UpdateAsync(user);
        }

        return user.TotpSecret;
    }

    public async Task<bool> ValidateTotpCodeAsync(Guid userId, string code)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || string.IsNullOrEmpty(user.TotpSecret))
        {
            return false;
        }

        return _totpService.ValidateCode(user.TotpSecret, code);
    }

    public async Task MarkTotpEnabledAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is not null)
        {
            user.TotpEnabled = true;
            await _userManager.UpdateAsync(user);
        }
    }

    private static ApplicationUserInfo ToInfo(ApplicationUser u) => new(
        u.Id, u.UserName ?? string.Empty, u.FullName, u.Role, u.BusinessId, u.IsActive, u.TotpEnabled);
}
