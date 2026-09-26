using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaneHesap.Application.Common.Exceptions;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Enums;
using TaneHesap.Infrastructure.Identity;
using TaneHesap.Infrastructure.Persistence;

namespace TaneHesap.Infrastructure.Services;

/// <summary>
/// IIdentityService'in ASP.NET Core Identity (UserManager) üzerinden implementasyonu.
/// bkz. Proje Raporu bölüm 2, 3.7.
/// </summary>
public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;

    public IdentityService(UserManager<ApplicationUser> userManager, ApplicationDbContext dbContext)
    {
        _userManager = userManager;
        _dbContext = dbContext;
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
            IsActive = true
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

    public async Task<bool> UpdateUserAsync(Guid userId, Guid businessId, UserRole role, string? fullName, string? username, string? newPassword)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == userId && u.BusinessId == businessId && u.Role == role);
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
            EnsureSucceeded(await _userManager.SetUserNameAsync(user, username));
        }

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            EnsureSucceeded(await _userManager.ResetPasswordAsync(user, token, newPassword));
        }

        EnsureSucceeded(await _userManager.UpdateAsync(user));
        return true;
    }

    /// <summary>Identity hataları (kullanıcı adı çakışması, zayıf şifre) sessizce yutulmaz; kullanıcıya 400 olarak döner.</summary>
    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new ValidationAppException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }
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

    public async Task<bool> DeleteUserAsync(Guid userId, Guid businessId, UserRole role)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == userId && u.BusinessId == businessId && u.Role == role);
        if (user is null)
        {
            return false;
        }

        var sessions = await _dbContext.RefreshTokens.Where(t => t.UserId == userId).ToListAsync();
        _dbContext.RefreshTokens.RemoveRange(sessions);
        await _dbContext.SaveChangesAsync();

        var result = await _userManager.DeleteAsync(user);
        return result.Succeeded;
    }

    public async Task<ApplicationUserInfo?> ValidatePasswordAsync(string username, string password)
    {
        var user = await _userManager.FindByNameAsync(username);
        if (user is null)
        {
            return null;
        }

        // Kaba kuvvet koruması: üst üste hatalı denemede hesap geçici olarak kilitlenir (bkz. DependencyInjection Lockout).
        if (await _userManager.IsLockedOutAsync(user))
        {
            return null;
        }

        var isValid = await _userManager.CheckPasswordAsync(user, password);
        if (!isValid)
        {
            await _userManager.AccessFailedAsync(user);
            return null;
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        return ToInfo(user);
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

    public async Task<List<ApplicationUserInfo>> GetUsersByBusinessAsync(Guid businessId)
    {
        var users = await _userManager.Users.Where(u => u.BusinessId == businessId).ToListAsync();
        return users.Select(ToInfo).ToList();
    }

    public async Task<List<ApplicationUserInfo>> GetAdminsByBusinessAsync(Guid businessId)
    {
        var users = await _userManager.Users
            .Where(u => u.BusinessId == businessId && u.Role == UserRole.Admin && u.IsActive)
            .ToListAsync();

        return users.Select(ToInfo).ToList();
    }

    public async Task<List<ApplicationUserInfo>> GetAllAdminsByBusinessAsync(Guid businessId)
    {
        var users = await _userManager.Users
            .Where(u => u.BusinessId == businessId && u.Role == UserRole.Admin)
            .ToListAsync();

        return users.Select(ToInfo).ToList();
    }

    public Task<bool> AnySuperAdminExistsAsync()
        => _userManager.Users.AnyAsync(u => u.Role == UserRole.SuperAdmin);

    private static ApplicationUserInfo ToInfo(ApplicationUser u) => new(
        u.Id, u.UserName ?? string.Empty, u.FullName, u.Role, u.BusinessId, u.IsActive);
}
