using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.DailyClosing;
using TaneHesap.Domain.Entities;

namespace TaneHesap.Application.Reports;

public class ClosingVarianceTotals : IClosingVarianceTotals
{
    private readonly IUnitOfWork _unitOfWork;

    public ClosingVarianceTotals(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<decimal> SumAsync(Guid businessId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var rows = await _unitOfWork.Repository<TreasuryTransaction>().ListAsync(t =>
            t.BusinessId == businessId
            && t.SourceReferenceType == DailyClosingService.VarianceSourceType
            && t.TransactionDate >= fromDate && t.TransactionDate <= toDate, ct);
        return rows.Sum(t => t.Amount);
    }
}
