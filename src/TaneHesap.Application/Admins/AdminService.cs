using TaneHesap.Application.Common.Exceptions;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Admins;

public class AdminService : IAdminService
{
    private readonly IIdentityService _identityService;
    private readonly IUnitOfWork _unitOfWork;

    public AdminService(IIdentityService identityService, IUnitOfWork unitOfWork)
    {
        _identityService = identityService;
        _unitOfWork = unitOfWork;
    }

    public async Task<List<AdminDto>> GetAllAsync(Guid businessId, CancellationToken ct = default)
    {
        await EnsureBusinessExistsAsync(businessId, ct);
        var admins = await _identityService.GetAllAdminsByBusinessAsync(businessId);
        return admins.Select(ToDto).ToList();
    }

    public async Task<AdminDto> CreateAsync(Guid businessId, CreateAdminRequest request, CancellationToken ct = default)
    {
        await EnsureBusinessExistsAsync(businessId, ct);

        var result = await _identityService.CreateAdminOrSuperAdminAsync(
            request.Username, request.Password, request.FullName, UserRole.Admin, businessId);

        if (!result.Succeeded || result.UserId is null)
        {
            throw new ConflictAppException(string.Join("; ", result.Errors));
        }

        return new AdminDto(result.UserId.Value, request.Username, request.FullName, true);
    }

    public async Task DeleteAsync(Guid businessId, Guid adminId, CancellationToken ct = default)
    {
        if (!await _identityService.DeleteUserAsync(adminId, businessId, UserRole.Admin))
        {
            throw new NotFoundException("Admin", adminId);
        }
    }

    public async Task<AdminDto> UpdateAsync(Guid businessId, Guid adminId, UpdateAdminRequest request, CancellationToken ct = default)
    {
        var updated = await _identityService.UpdateUserAsync(
            adminId, businessId, UserRole.Admin, request.FullName, request.Username, request.Password);

        if (!updated)
        {
            throw new NotFoundException("Admin", adminId);
        }

        await _identityService.SetActiveAsync(adminId, request.IsActive);

        var user = await _identityService.GetByIdAsync(adminId)
            ?? throw new NotFoundException("Admin", adminId);

        return ToDto(user);
    }

    private async Task EnsureBusinessExistsAsync(Guid businessId, CancellationToken ct)
    {
        var business = await _unitOfWork.Repository<Business>().GetByIdAsync(businessId, ct);
        if (business is null)
        {
            throw new NotFoundException(nameof(Business), businessId);
        }
    }

    private static AdminDto ToDto(ApplicationUserInfo u) => new(u.UserId, u.Username, u.FullName, u.IsActive);
}
