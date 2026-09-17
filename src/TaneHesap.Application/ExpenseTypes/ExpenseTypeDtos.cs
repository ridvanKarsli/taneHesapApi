using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.ExpenseTypes;

public record ExpenseTypeDto(Guid Id, string Name, string Unit, ExpenseCategory Category, bool IsActive);

public record CreateExpenseTypeRequest(string Name, string Unit, ExpenseCategory Category);

public record UpdateExpenseTypeRequest(string Name, string Unit, ExpenseCategory Category, bool IsActive);
