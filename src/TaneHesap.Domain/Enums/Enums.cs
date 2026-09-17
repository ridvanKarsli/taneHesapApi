namespace TaneHesap.Domain.Enums;

/// <summary>Sistemdeki kullanıcı rolleri. bkz. Proje Raporu bölüm 2.</summary>
public enum UserRole
{
    SuperAdmin = 0,
    Admin = 1,
    Employee = 2
}

/// <summary>Ödeme şekli — nakit/kart ayrımı raporlamada kullanılır.</summary>
public enum PaymentMethod
{
    Cash = 0,
    Card = 1
}

/// <summary>Gider türü kategorisi (ADMIN tarafından tanımlanan gider türlerinin üst kategorisi).</summary>
public enum ExpenseCategory
{
    Material = 0,
    FixedExpense = 1,
    Other = 2
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
    DailyLossWarning = 2
}
