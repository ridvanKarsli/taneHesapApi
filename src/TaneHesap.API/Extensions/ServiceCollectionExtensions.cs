using TaneHesap.Application.Auth;
using TaneHesap.Application.Businesses;
using TaneHesap.Application.Dishes;
using TaneHesap.Application.Employees;
using TaneHesap.Application.ExpenseTypes;
using TaneHesap.Application.Expenses;
using TaneHesap.Application.Ingredients;

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
        services.AddScoped<IExpenseTypeService, ExpenseTypeService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IIngredientService, IngredientService>();
        services.AddScoped<IDishService, DishService>();

        return services;
    }
}
