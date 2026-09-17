using TaneHesap.Application.Common.Exceptions;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Entities;

namespace TaneHesap.Application.Businesses;

public class BusinessService : IBusinessService
{
    private readonly IUnitOfWork _unitOfWork;

    public BusinessService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<BusinessDto>> GetAllAsync(CancellationToken ct = default)
    {
        var businesses = await _unitOfWork.Repository<Business>().ListAsync(ct: ct);
        return businesses
            .OrderBy(b => b.Name)
            .Select(ToDto)
            .ToList();
    }

    public async Task<BusinessDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var business = await _unitOfWork.Repository<Business>().GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Business), id);
        return ToDto(business);
    }

    public async Task<BusinessDto> CreateAsync(CreateBusinessRequest request, Guid createdByUserId, CancellationToken ct = default)
    {
        var business = new Business
        {
            Name = request.Name,
            Address = request.Address,
            IsActive = true,
            CreatedByUserId = createdByUserId
        };

        await _unitOfWork.Repository<Business>().AddAsync(business, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ToDto(business);
    }

    public async Task<BusinessDto> UpdateAsync(Guid id, UpdateBusinessRequest request, Guid updatedByUserId, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<Business>();
        var business = await repo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Business), id);

        business.Name = request.Name;
        business.Address = request.Address;
        business.IsActive = request.IsActive;
        business.UpdatedByUserId = updatedByUserId;
        business.UpdatedAtUtc = DateTime.UtcNow;

        repo.Update(business);
        await _unitOfWork.SaveChangesAsync(ct);

        return ToDto(business);
    }

    private static BusinessDto ToDto(Business b) => new(b.Id, b.Name, b.Address, b.IsActive, b.CreatedAtUtc);
}
