using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Entities;

namespace TaneHesap.Application.Reports;

public class ClosingVarianceTotals : IClosingVarianceTotals
{
    /// <summary>
    /// Kaldırılan gün sonu sayım modülünün kasaya yazdığı fark hareketleri. Yeni kayıt oluşmaz; eski kayıtlar kasada
    /// durduğu sürece kâr da onları içermeli ki kasa ile kâr tutarlı kalsın.
    /// </summary>
    public const string VarianceSourceType = "DailyClosingVariance";

    private readonly IUnitOfWork _unitOfWork;

    public ClosingVarianceTotals(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<decimal> SumAsync(Guid businessId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var rows = await _unitOfWork.Repository<TreasuryTransaction>().ListAsync(t =>
            t.BusinessId == businessId
            && t.SourceReferenceType == VarianceSourceType
            && t.TransactionDate >= fromDate && t.TransactionDate <= toDate, ct);
        return rows.Sum(t => t.Amount);
    }
}
