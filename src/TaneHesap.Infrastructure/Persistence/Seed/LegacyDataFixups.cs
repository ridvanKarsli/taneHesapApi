using Microsoft.EntityFrameworkCore;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Infrastructure.Persistence.Seed;

/// <summary>
/// Şema migration'ı ile taşınamayan, veri düzeyindeki geriye dönük düzeltmeler. Her açılışta çalışır ve
/// idempotenttir (etkilenen satır yoksa hiçbir şey yapmaz). Kural değişikliği: "Sabit gider" kategorisi
/// (eski enum değeri 1) kaldırıldı — mevcut gider türleri "Diğer"e taşınır (bkz. Proje Raporu bölüm 3.2).
/// </summary>
public static class LegacyDataFixups
{
    private const int RemovedFixedExpenseCategory = 1;

    public static Task ApplyAsync(ApplicationDbContext dbContext, CancellationToken ct = default)
        => dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "ExpenseTypes" SET "Category" = {(int)ExpenseCategory.Other} WHERE "Category" = {RemovedFixedExpenseCategory}""", ct);
}
