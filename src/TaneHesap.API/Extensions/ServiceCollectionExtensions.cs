using TaneHesap.Application.Admins;
using TaneHesap.Application.AuditLogs;
using TaneHesap.Application.Auth;
using TaneHesap.Application.Businesses;
using TaneHesap.Application.DailyClosing;
using TaneHesap.Application.DailySales;
using TaneHesap.Application.Dishes;
using TaneHesap.Application.Employees;
using TaneHesap.Application.ExpenseTypes;
using TaneHesap.Application.Expenses;
using TaneHesap.Application.Ingredients;
using TaneHesap.Application.Notifications;
using TaneHesap.Application.Platforms;
using TaneHesap.Application.RecurringExpenses;
using TaneHesap.Application.Reports;
using TaneHesap.Application.Stock;
using TaneHesap.Application.Suppliers;
using TaneHesap.Application.Treasury;

namespace TaneHesap.API.Extensions;

/// <summary>
/// Application katmanındaki servislerin DI kaydı. Bilinçli olarak burada (composition root / API
/// katmanı) tutulur, böylece Application katmanı bir DI konteyner paketine bağımlı olmaz ve bu
/// sandbox ortamında NuGet erişimi olmadan da derlenip doğrulanabilir.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IBusinessService, BusinessService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IExpenseTypeService, ExpenseTypeService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IEmployeeWalletService, EmployeeWalletService>();
        services.AddScoped<IIngredientService, IngredientService>();
        services.AddScoped<IDishService, DishService>();
        services.AddScoped<IStockMovementService, StockMovementService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IRecurringExpenseService, RecurringExpenseService>();
        services.AddScoped<IRecurringExpenseReminderService, RecurringExpenseReminderService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IPlatformService, PlatformService>();
        services.AddScoped<ITreasuryService, TreasuryService>();
        services.AddScoped<IExpenseTreasuryPoster, ExpenseTreasuryPoster>();
        services.AddScoped<IExpectedConsumptionCalculator, ExpectedConsumptionCalculator>();
        // Satış verisi değişince yeniden hesaplanan türetilmiş kayıtlar — sırayla çağrılır (bkz. IDailySalesSideEffect).
        services.AddScoped<IDailySalesSideEffect, PlatformCommissionExpensePoster>();
        services.AddScoped<IDailySalesSideEffect, SalesStockConsumptionPoster>();
        services.AddScoped<IDailySalesSideEffect, SalesTreasuryPoster>();
        services.AddScoped<IDailySalesService, DailySalesService>();
        services.AddScoped<IDailyClosingService, DailyClosingService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IMonthlyReportService, MonthlyReportService>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        return services;
    }
}
