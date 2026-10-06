using Microsoft.EntityFrameworkCore;
using TaneHesap.Application.Platforms;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Infrastructure.Persistence.Seed;

/// <summary>
/// Şema migration'ı ile taşınamayan, veri düzeyindeki geriye dönük düzeltmeler. Her açılışta çalışır ve
/// idempotenttir (etkilenen satır yoksa hiçbir şey yapmaz).
/// - "Sabit gider" kategorisi (eski enum değeri 1) kaldırıldı — mevcut gider türleri "Diğer"e taşınır (bölüm 3.2).
/// - "Kart Komisyonu" adı "POS Komisyonu" oldu — mevcut otomatik gider türü ve açıklamalar yeniden adlandırılır.
/// - Düzenli gider ödemeleri "Düzenli Gider" türü yerine düzenli giderin adını taşır; eski ödemeler taşınır.
/// - Yemeksepeti ve Trendyol Go platformları her işletmede hazır gelir; eksik olan işletmelere eklenir.
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

        // Düzenli gider ödemeleri eskiden tek bir "Düzenli Gider" türüne yazılıyordu; artık tür = düzenli giderin adı ("Kira").
        // Eski ödemeler için ad başına tür açılır (yoksa) ve gider o türe taşınır.
        await db.ExecuteSqlRawAsync(
            """
            INSERT INTO "ExpenseTypes" ("Id", "BusinessId", "Name", "Unit", "Category", "IsActive", "CreatedAtUtc")
            SELECT gen_random_uuid(), r."BusinessId", TRIM(r."Name"), 'adet', 2, TRUE, NOW()
            FROM "RecurringExpenses" AS r
            WHERE EXISTS (
                    SELECT 1 FROM "Expenses" AS e
                    JOIN "RecurringExpensePayments" AS p ON p."Id" = e."SourceReferenceId"
                    JOIN "ExpenseTypes" AS old ON old."Id" = e."ExpenseTypeId"
                    WHERE e."SourceReferenceType" = 'RecurringExpensePayment' AND p."RecurringExpenseId" = r."Id" AND old."Name" = 'Düzenli Gider')
              AND NOT EXISTS (SELECT 1 FROM "ExpenseTypes" AS t WHERE t."BusinessId" = r."BusinessId" AND t."Name" = TRIM(r."Name"))
            """, ct);

        await db.ExecuteSqlRawAsync(
            """
            UPDATE "Expenses" AS e SET "ExpenseTypeId" = t."Id"
            FROM "RecurringExpensePayments" AS p
            JOIN "RecurringExpenses" AS r ON r."Id" = p."RecurringExpenseId"
            JOIN "ExpenseTypes" AS t ON t."BusinessId" = r."BusinessId" AND t."Name" = TRIM(r."Name")
            JOIN "ExpenseTypes" AS old ON old."Name" = 'Düzenli Gider'
            WHERE e."SourceReferenceType" = 'RecurringExpensePayment' AND e."SourceReferenceId" = p."Id" AND e."ExpenseTypeId" = old."Id"
            """, ct);

        await EnsureDefaultPlatformsAsync(dbContext, ct);
    }

    /// <summary>
    /// Yemeksepeti ve Trendyol Go her işletmede hazır gelir (yeni işletmede BusinessService açar). Var olan işletmelerde
    /// adında ilgili anahtar kelime geçen bir platform yoksa %0 komisyonla eklenir; süper admin oranı sonra girer.
    /// </summary>
    private static async Task EnsureDefaultPlatformsAsync(ApplicationDbContext dbContext, CancellationToken ct)
    {
        foreach (var definition in DefaultPlatforms.All)
        {
            var keywordPattern = string.Join("|", definition.Keywords.Select(k => k.Replace(" ", @"\s*")));
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO "Platforms" ("Id", "BusinessId", "Name", "CommissionPercentage", "IsActive", "CreatedAtUtc")
                SELECT gen_random_uuid(), b."Id", {definition.Name}, 0, TRUE, NOW()
                FROM "Businesses" AS b
                WHERE NOT EXISTS (SELECT 1 FROM "Platforms" AS p WHERE p."BusinessId" = b."Id" AND p."Name" ~* {keywordPattern})
                """, ct);
        }
    }
}
