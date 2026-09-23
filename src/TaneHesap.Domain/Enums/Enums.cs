namespace TaneHesap.Domain.Enums;

/// <summary>Sistemdeki kullanıcı rolleri. bkz. Proje Raporu bölüm 2.</summary>
public enum UserRole
{
    SuperAdmin = 0,
    Admin = 1,
    Employee = 2
}

/// <summary>
/// Ödeme şekli. Satışta nakit/kart ayrımı raporlamada kullanılır; giderde paranın hangi kasadan
/// çıktığını belirler: Cash = nakit kasası, Card = tanımlı bir kredi kartı (limitten düşer),
/// Bank = kart kasası/banka hesabı (havale, platform komisyonu kesintisi vb.). bkz. Proje Raporu bölüm 3.15.
/// </summary>
public enum PaymentMethod
{
    Cash = 0,
    Card = 1,
    Bank = 2
}

/// <summary>
/// Gider türü kategorisi (ADMIN tarafından tanımlanan gider türlerinin üst kategorisi). "Sabit gider"
/// (eski değer 1) kaldırıldı — sabit giderler Düzenli Giderler modülünde yönetilir; eski kayıtlar
/// açılışta Other'a taşınır (bkz. LegacyDataFixups). Personnel: çalışana yapılan ödemeler (cüzdandan düşer).
/// </summary>
public enum ExpenseCategory
{
    Material = 0,
    Other = 2,
    Personnel = 3
}

/// <summary>Stok hareketi tipi.</summary>
public enum StockMovementType
{
    Purchase = 0,
    SaleConsumption = 1,
    ManualAdjustment = 2,
    Waste = 3
}

/// <summary>Satış kanalı — dükkan içi mi, paket servis platformu üzerinden mi.</summary>
public enum SalesChannel
{
    InStore = 0,
    Platform = 1
}

/// <summary>Düzenli gider periyodu.</summary>
public enum RecurringPeriod
{
    Weekly = 0,
    Monthly = 1,
    Yearly = 2
}

/// <summary>Uygulama içi (in-app) bildirim tipi. bkz. Proje Raporu bölüm 3.13.</summary>
public enum NotificationType
{
    LowStock = 0,
    RecurringExpenseReminder = 1,
    DailyLossWarning = 2,
    MonthlyReport = 3
}

/// <summary>İşletme kasası hesabı: nakit kasası, kart kasası (banka/POS hesabı) veya bir kredi kartı. bkz. bölüm 3.15.</summary>
public enum TreasuryAccount
{
    Cash = 0,
    Bank = 1,
    CreditCard = 2
}

/// <summary>Kasa hareketinin kaynağı.</summary>
public enum TreasuryTransactionKind
{
    /// <summary>Gün sonu satışlarından gelen gelir (nakit → nakit kasası, kart → kart kasası).</summary>
    SalesRevenue = 0,
    /// <summary>Kart satışlarından bankanın kestiği komisyon (Business.CardFeePercentage).</summary>
    CardFee = 1,
    /// <summary>Bir gider kaydının ödemesi.</summary>
    Expense = 2,
    /// <summary>Nakit ↔ kart kasası arası transfer.</summary>
    Transfer = 3,
    /// <summary>Kredi kartı borcunun kart kasasından ödenmesi (limit geri açılır).</summary>
    CardPayment = 4,
    /// <summary>Açılış bakiyesi / sayım düzeltmesi.</summary>
    ManualAdjustment = 5
}
