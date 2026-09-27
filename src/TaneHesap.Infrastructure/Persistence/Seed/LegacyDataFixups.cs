using Microsoft.EntityFrameworkCore;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Infrastructure.Persistence.Seed;

/// <summary>
/// Şema migration'ı ile taşınamayan, veri düzeyindeki geriye dönük düzeltmeler. Her açılışta çalışır ve
/// idempotenttir (etkilenen satır yoksa hiçbir şey yapmaz).
/// - "Sabit gider" kategorisi (eski enum değeri 1) kaldırıldı — mevcut gider türleri "Diğer"e taşınır (bölüm 3.2).
/// - "Kart Komisyonu" adı "POS Komisyonu" oldu — mevcut otomatik gider türü ve açıklamalar yeniden adlandırılır.
/// </summary>
public static class LegacyDataFixups
{
    private const int RemovedFixedExpenseCategory = 1;

    public static async Task ApplyAsync(ApplicationDbContext dbContext, CancellationToken ct = default)
    {
        var db = dbContext.Database;

        await db.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "ExpenseTypes" SET "Category" = {(int)ExpenseCategory.Other} WHERE "Category" = {RemovedFixedExpenseCategory}""", ct);

        await db.ExecuteSqlRawAsync(
            """
            UPDATE "ExpenseTypes" AS t SET "Name" = 'POS Komisyonu'
            WHERE t."Name" = 'Kart Komisyonu'
              AND NOT EXISTS (SELECT 1 FROM "ExpenseTypes" AS o WHERE o."BusinessId" = t."BusinessId" AND o."Name" = 'POS Komisyonu')
            """, ct);

        await db.ExecuteSqlRawAsync(
            """
            UPDATE "Expenses" SET "Description" = REPLACE("Description", 'Kart satışları banka komisyonu', 'POS komisyonu')
            WHERE "Description" LIKE 'Kart satışları banka komisyonu%'
            """, ct);

        await db.ExecuteSqlRawAsync(
            """
            UPDATE "TreasuryTransactions" SET "Description" = REPLACE("Description", 'Kart satışları banka komisyonu', 'POS komisyonu')
            WHERE "Description" LIKE 'Kart satışları banka komisyonu%'
            """, ct);
    }
}
