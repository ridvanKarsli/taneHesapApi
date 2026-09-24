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
        new(nameof(DailyActualEntry), "Gün sonu kapanışı"),
        new(nameof(SupplierPurchase), "Malzeme alışı"),
        new(nameof(SupplierPayment), "Tedarikçi ödemesi"),
        new(nameof(RecurringExpensePayment), "Düzenli gider ödemesi"),
        new(nameof(EmployeeWorkLog), "Çalışma saati"),
        new(nameof(TreasuryTransaction), "Kasa hareketi"),
        new(nameof(StockMovement), "Stok hareketi"),
        new(nameof(PaymentCard), "Kart"),
    };

    private static readonly string[] AmountKeys = { "Amount", "TotalAmount", "PaidAmount", "ActualRevenue", "QuantityChange", "Hours", "Limit" };
    private static readonly string[] DateKeys = { "ExpenseDate", "SaleDate", "EntryDate", "PurchaseDate", "PaymentDate", "WorkDate", "TransactionDate", "PeriodStartDate" };
    private static readonly string[] TextKeys = { "Description", "Note", "Name" };
    /// <summary>Değişiklik özetinde gösterilen alanlar ve işletme sahibinin anlayacağı adları; listede olmayan (teknik) alanlar gizlenir.</summary>
    private static readonly Dictionary<string, string> FieldLabels = new()
    {
        ["Amount"] = "Tutar", ["TotalAmount"] = "Toplam", ["PaidAmount"] = "Ödenen", ["ActualRevenue"] = "Gerçek gelir",
        ["Quantity"] = "Miktar", ["QuantityChange"] = "Stok değişimi", ["Hours"] = "Saat", ["HourlyWage"] = "Saatlik ücret",
        ["UnitPrice"] = "Birim fiyat", ["Limit"] = "Limit", ["Description"] = "Açıklama", ["Note"] = "Not", ["Name"] = "Ad",
        ["ExpenseDate"] = "Tarih", ["SaleDate"] = "Tarih", ["PurchaseDate"] = "Tarih", ["PaymentDate"] = "Tarih",
        ["WorkDate"] = "Tarih", ["TransactionDate"] = "Tarih", ["EntryDate"] = "Tarih", ["PaidDate"] = "Ödeme tarihi",
        ["PaymentMethod"] = "Ödeme şekli", ["IsActive"] = "Aktif", ["IsPaid"] = "Ödendi", ["DiscountAmount"] = "İndirim",
    };

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

        return logs
            .OrderByDescending(l => l.TimestampUtc)
            .Select(l => ToEntry(l, userNames.GetValueOrDefault(l.UserId, "Sistem / silinmiş kullanıcı"), kindLabels[l.EntityName]))
            .ToList();
    }

    private static ActivityEntryDto ToEntry(AuditLog log, string userName, string kind)
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

        return new ActivityEntryDto(log.Id, log.TimestampUtc, log.UserId, userName, action, log.EntityName, kind, log.EntityId, summary, ReadAmount(current));
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
            .Select(p => $"{FieldLabels[p.Name]}: {FormatField(p.Name, Get(oldValues.Value, p.Name))} → {FormatField(p.Name, p.Value)}")
            .ToList();
        return changes.Count == 0 ? "Küçük düzenleme" : string.Join(", ", changes);
    }

    private static decimal? ReadAmount(JsonElement? element)
    {
        if (element is null)
        {
            return null;
        }

        var value = AmountKeys.Select(k => Get(element.Value, k)).FirstOrDefault(v => v is { ValueKind: JsonValueKind.Number });
        return value?.GetDecimal();
    }

    private static string FormatField(string key, JsonElement? value)
    {
        if (value is null) return "—";
        return key == "PaymentMethod" && value.Value.ValueKind == JsonValueKind.Number ? PaymentLabel(value.Value.GetInt32()) : FormatValue(value.Value);
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
        PaymentMethod.Bank => "Kart kasası",
        _ => "",
    };

    private static string RoleLabel(UserRole role) => role switch
    {
        UserRole.Admin => "İşletme sahibi",
        UserRole.Employee => "Çalışan",
        _ => "Süper yönetici",
    };

    private static JsonDocument? Parse(string? json) => string.IsNullOrWhiteSpace(json) ? null : JsonDocument.Parse(json);
}
