using System.Globalization;
using System.Text.Json;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Activity;

/// <summary>
/// AuditLog satırlarını (JSON eski/yeni değerler) insan diline çevirir. Yeni bir kayıt türünü izlemeye
/// almak = <see cref="Kinds"/> tablosuna bir satır (Open/Closed); JSON alan adları entity özellik adlarıdır.
/// </summary>
public class ActivityService : IActivityService
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>İzlenen entity → Türkçe ad. Sıra, filtre listesindeki sırayı belirler.</summary>
    private static readonly ActivityKindDto[] Kinds =
    {
        new(nameof(Expense), "Gider"),
        new(nameof(DailySalesEntry), "Satış"),
        new(nameof(DailyIncomeVerification), "Gelir doğrulama"),
        new(nameof(TreasuryTransaction), "Kasa hareketi"),
        new(nameof(Business), "İşletme ayarı"),
        new(nameof(PaymentCard), "Kart"),
        new(nameof(SupplierPurchase), "Malzeme alışı"),
        new(nameof(SupplierPayment), "Tedarikçi ödemesi"),
        new(nameof(Supplier), "Tedarikçi"),
        new(nameof(RecurringExpense), "Düzenli gider tanımı"),
        new(nameof(RecurringExpensePayment), "Düzenli gider ödemesi"),
        new(nameof(EmployeeWorkLog), "Çalışma saati"),
        new(nameof(EmployeeProfile), "Çalışan ücreti"),
        new(nameof(StockMovement), "Stok hareketi"),
        new(nameof(Ingredient), "Malzeme"),
        new(nameof(Dish), "Ürün"),
        new(nameof(DishSize), "Ürün boyu / fiyatı"),
        new(nameof(ExpenseType), "Gider türü"),
        new(nameof(Platform), "Paket servis platformu"),
        new(nameof(DailyActualEntry), "Gün sonu kapanışı (eski)"),
    };

    private static readonly string[] AmountKeys = { "Amount", "TotalAmount", "PaidAmount", "ActualRevenue", "QuantityChange", "Hours", "Limit", "SalePrice", "HourlyWage" };
    private static readonly string[] DateKeys = { "ExpenseDate", "SaleDate", "EntryDate", "PurchaseDate", "PaymentDate", "WorkDate", "TransactionDate", "PeriodStartDate", "VerificationDate", "StartDate" };
    private static readonly string[] TextKeys = { "Description", "Note", "Name", "ProductName" };
    /// <summary>Değişiklik özetinde gösterilen alanlar ve işletme sahibinin anlayacağı adları; listede olmayan (teknik) alanlar gizlenir.</summary>
    private static readonly Dictionary<string, string> FieldLabels = new()
    {
        ["Amount"] = "Tutar", ["TotalAmount"] = "Toplam", ["PaidAmount"] = "Ödenen", ["ActualRevenue"] = "Gerçek gelir",
        ["Quantity"] = "Miktar", ["QuantityChange"] = "Stok değişimi", ["Hours"] = "Saat", ["HourlyWage"] = "Saatlik ücret",
        ["UnitPrice"] = "Birim fiyat", ["Limit"] = "Limit", ["Description"] = "Açıklama", ["Note"] = "Not", ["Name"] = "Ad",
        ["ExpenseDate"] = "Tarih", ["SaleDate"] = "Tarih", ["PurchaseDate"] = "Tarih", ["PaymentDate"] = "Tarih",
        ["WorkDate"] = "Tarih", ["TransactionDate"] = "Tarih", ["EntryDate"] = "Tarih", ["PaidDate"] = "Ödeme tarihi",
        ["PaymentMethod"] = "Ödeme şekli", ["IsActive"] = "Aktif", ["IsPaid"] = "Ödendi", ["DiscountAmount"] = "İndirim",
        ["VerificationDate"] = "Tarih", ["ActualCash"] = "Gerçek nakit", ["ActualCard"] = "Gerçek kart",
        ["CardFeePercentage"] = "POS komisyonu (%)", ["CommissionPercentage"] = "Komisyon (%)", ["SalePrice"] = "Satış fiyatı",
        ["CurrentUnitPrice"] = "Birim fiyat", ["MinimumStockThreshold"] = "Minimum stok", ["Unit"] = "Birim",
        ["IntervalCount"] = "Periyot (kaç)", ["StartDate"] = "Başlangıç", ["ContactInfo"] = "İletişim", ["Address"] = "Adres",
        ["Account"] = "Hesap", ["ExpenseTypeId"] = "Gider türü", ["PaymentCardId"] = "Kart", ["EmployeeUserId"] = "Çalışan",
        ["IngredientId"] = "Malzeme", ["DishSizeId"] = "Ürün", ["ProductName"] = "Ürün adı",
    };

    /// <summary>
    /// Sistemin arka planda güncellediği alanlar: yalnızca bunlar değiştiyse (örn. satış yüklenince malzemenin stok
    /// miktarı, ödeme gelince alışın "tamamı ödendi" işareti) işlem geçmişinde ayrı satır gösterilmez.
    /// </summary>
    private static readonly HashSet<string> BackgroundFields = new()
    {
        "CurrentStockQuantity", "IsFullyPaid", "ExcelImportLogId", "UpdatedAtUtc", "UpdatedByUserId"
    };

    private static readonly Dictionary<string, string> PercentFields = new() { ["CardFeePercentage"] = "", ["CommissionPercentage"] = "" };

    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdentityService _identityService;

    public ActivityService(IUnitOfWork unitOfWork, IIdentityService identityService)
    {
        _unitOfWork = unitOfWork;
        _identityService = identityService;
    }

    public IReadOnlyList<ActivityKindDto> GetKinds() => Kinds;

    public async Task<List<ActivityUserDto>> GetUsersAsync(Guid businessId, CancellationToken ct = default)
    {
        var users = await _identityService.GetUsersByBusinessAsync(businessId);
        return users.OrderBy(u => u.Role).ThenBy(u => u.FullName)
            .Select(u => new ActivityUserDto(u.UserId, u.FullName, RoleLabel(u.Role))).ToList();
    }

    public async Task<List<ActivityEntryDto>> GetAsync(Guid businessId, ActivityQuery query, CancellationToken ct = default)
    {
        var fromUtc = query.FromDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = query.ToDate?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var trackedNames = Kinds.Select(k => k.EntityName).ToArray();

        var logs = await _unitOfWork.Repository<AuditLog>().ListAsync(l =>
            l.BusinessId == businessId
            && trackedNames.Contains(l.EntityName)
            && (query.EntityName == null || l.EntityName == query.EntityName)
            && (query.UserId == null || l.UserId == query.UserId)
            && (fromUtc == null || l.TimestampUtc >= fromUtc)
            && (toUtc == null || l.TimestampUtc < toUtc), ct);

        var userNames = (await _identityService.GetUsersByBusinessAsync(businessId)).ToDictionary(u => u.UserId, u => u.FullName);
        var kindLabels = Kinds.ToDictionary(k => k.EntityName, k => k.Label);

        // İşletmeye "girmiş" SUPER_ADMIN işletme kullanıcısı değildir; adı ayrıca bulunur ve işaretlenir.
        foreach (var userId in logs.Select(l => l.UserId).Distinct().Where(id => !userNames.ContainsKey(id)))
        {
            var outsider = await _identityService.GetByIdAsync(userId);
            userNames[userId] = outsider is null
                ? "Sistem / silinmiş kullanıcı"
                : outsider.Role == UserRole.SuperAdmin ? $"{outsider.FullName} (süper yönetici)" : outsider.FullName;
        }

        // En yeni en üstte. Aynı anda yazılan kayıtlarda (tek işlemde birden çok satır) sıra sabit kalsın diye Id ile de sıralanır.
        return logs
            .OrderByDescending(l => l.TimestampUtc).ThenByDescending(l => l.Id)
            .Select(l => ToEntry(l, userNames[l.UserId], kindLabels[l.EntityName]))
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();
    }

    /// <summary>Kaydı insan diline çevirir; yalnızca teknik alanları değişen güncellemeler (örn. satıştan stok miktarı) için null.</summary>
    private static ActivityEntryDto? ToEntry(AuditLog log, string userName, string kind)
    {
        using var newDoc = Parse(log.NewValuesJson);
        using var oldDoc = Parse(log.OldValuesJson);
        var current = newDoc?.RootElement ?? oldDoc?.RootElement;

        var (action, summary) = log.ActionType switch
        {
            "Added" => ("Ekledi", Describe(current)),
            "Deleted" => ("Sildi", Describe(current)),
            _ => ("Değiştirdi", DescribeChanges(oldDoc?.RootElement, newDoc?.RootElement)),
        };

        if (action == "Değiştirdi")
        {
            var changed = newDoc?.RootElement.EnumerateObject().Select(p => p.Name).ToList() ?? new List<string>();
            if (changed.All(BackgroundFields.Contains))
            {
                return null;
            }

            if (summary.Length == 0)
            {
                summary = "Diğer bilgiler güncellendi";
            }
        }

        return new ActivityEntryDto(log.Id, log.TimestampUtc, log.UserId, userName, action, log.EntityName,
            DetailedKind(log.EntityName, kind, current), log.EntityId, summary, ReadAmount(log.EntityName, current));
    }

    /// <summary>Kasa hareketinde türü ve hesabı açık yazar: "Kasa: Bakiye ayarı (Nakit kasası)".</summary>
    private static string DetailedKind(string entityName, string kind, JsonElement? values)
    {
        if (entityName != nameof(TreasuryTransaction) || values is null)
        {
            return kind;
        }

        var kindValue = Get(values.Value, "Kind");
        var account = Get(values.Value, "Account");
        var kindText = kindValue is { ValueKind: JsonValueKind.Number } ? TreasuryKindLabel(kindValue.Value.GetInt32()) : "Hareket";
        return account is { ValueKind: JsonValueKind.Number } ? $"Kasa: {kindText} ({AccountLabel(account.Value.GetInt32())})" : $"Kasa: {kindText}";
    }

    private static string Describe(JsonElement? element)
    {
        if (element is null)
        {
            return "";
        }

        var parts = new List<string>();
        var date = DateKeys.Select(k => Get(element.Value, k)).FirstOrDefault(v => v is not null);
        if (date is not null) parts.Add(FormatValue(date.Value));
        var text = TextKeys.Select(k => Get(element.Value, k)).FirstOrDefault(v => v is { ValueKind: JsonValueKind.String });
        if (text is not null) parts.Add(text.Value.GetString()!);
        var method = Get(element.Value, "PaymentMethod");
        if (method is { ValueKind: JsonValueKind.Number }) parts.Add(PaymentLabel(method.Value.GetInt32()));
        var cash = Get(element.Value, "ActualCash");
        var card = Get(element.Value, "ActualCard");
        if (cash is { ValueKind: JsonValueKind.Number } && card is { ValueKind: JsonValueKind.Number })
        {
            parts.Add($"Nakit ₺{FormatValue(cash.Value)}, Kart ₺{FormatValue(card.Value)}");
        }

        var fee = Get(element.Value, "CardFeePercentage") ?? Get(element.Value, "CommissionPercentage");
        if (fee is { ValueKind: JsonValueKind.Number }) parts.Add($"Komisyon %{FormatValue(fee.Value)}");
        return string.Join(" · ", parts);
    }

    private static string DescribeChanges(JsonElement? oldValues, JsonElement? newValues)
    {
        if (oldValues is null || newValues is null)
        {
            return "";
        }

        var changes = newValues.Value.EnumerateObject()
            .Where(p => FieldLabels.ContainsKey(p.Name))
            // Kimlik alanlarında (…Id) ham değer anlamsızdır; yalnızca değiştiği söylenir.
            .Select(p => p.Name.EndsWith("Id", StringComparison.Ordinal)
                ? $"{FieldLabels[p.Name]} değişti"
                : $"{FieldLabels[p.Name]}: {FormatField(p.Name, Get(oldValues.Value, p.Name))} → {FormatField(p.Name, p.Value)}")
            .ToList();
        return string.Join(", ", changes);
    }

    private static decimal? ReadAmount(string entityName, JsonElement? element)
    {
        if (element is null)
        {
            return null;
        }

        if (entityName == nameof(DailyIncomeVerification))
        {
            var cash = Get(element.Value, "ActualCash");
            var card = Get(element.Value, "ActualCard");
            return cash is { ValueKind: JsonValueKind.Number } && card is { ValueKind: JsonValueKind.Number }
                ? cash.Value.GetDecimal() + card.Value.GetDecimal()
                : null;
        }

        var value = AmountKeys.Select(k => Get(element.Value, k)).FirstOrDefault(v => v is { ValueKind: JsonValueKind.Number });
        return value?.GetDecimal();
    }

    private static string FormatField(string key, JsonElement? value)
    {
        if (value is null) return "—";
        if (value.Value.ValueKind == JsonValueKind.Number)
        {
            if (key == "PaymentMethod") return PaymentLabel(value.Value.GetInt32());
            if (key == "Account") return AccountLabel(value.Value.GetInt32());
            if (key == "IntervalCount") return value.Value.GetInt32().ToString(Tr);
            if (PercentFields.ContainsKey(key)) return "%" + value.Value.GetDecimal().ToString("0.##", Tr);
        }

        return FormatValue(value.Value);
    }

    private static JsonElement? Get(JsonElement element, string key)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null ? value : null;

    private static string FormatValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number => value.GetDecimal().ToString("N2", Tr),
        JsonValueKind.String when DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", out var d) => d.ToString("dd.MM.yyyy", Tr),
        JsonValueKind.String => value.GetString() ?? "",
        JsonValueKind.True => "evet",
        JsonValueKind.False => "hayır",
        _ => "—",
    };

    private static string PaymentLabel(int method) => (PaymentMethod)method switch
    {
        PaymentMethod.Cash => "Nakit",
        PaymentMethod.Card => "Kredi kartı",
        PaymentMethod.Bank => "Banka hesabı",
        _ => "",
    };

    private static string AccountLabel(int account) => (TreasuryAccount)account switch
    {
        TreasuryAccount.Cash => "Nakit kasası",
        TreasuryAccount.Bank => "Banka hesabı",
        TreasuryAccount.CreditCard => "Kredi kartı",
        _ => "",
    };

    private static string TreasuryKindLabel(int kind) => (TreasuryTransactionKind)kind switch
    {
        TreasuryTransactionKind.SalesRevenue => "Satış geliri",
        TreasuryTransactionKind.Expense => "Gider ödemesi",
        TreasuryTransactionKind.Transfer => "Transfer",
        TreasuryTransactionKind.CardPayment => "Kart borcu ödemesi",
        TreasuryTransactionKind.ManualAdjustment => "Bakiye ayarı",
        _ => "Hareket",
    };

    private static string RoleLabel(UserRole role) => role switch
    {
        UserRole.Admin => "İşletme sahibi",
        UserRole.Employee => "Çalışan",
        _ => "Süper yönetici",
    };

    private static JsonDocument? Parse(string? json) => string.IsNullOrWhiteSpace(json) ? null : JsonDocument.Parse(json);
}
