namespace TaneHesap.Application.Employees;

/// <param name="Balance">Cüzdan bakiyesi = hak ediş (çalışma saatleri) − yapılan ödemeler. Pozitif: işletme çalışana borçlu
/// (çalışanın alacağı); negatif: çalışana fazla ödenmiş (çalışanın vereceği).</param>
public record EmployeeDto(Guid Id, string Username, string FullName, bool IsActive, decimal HourlyWage, decimal Balance);

public record CreateEmployeeRequest(string Username, string Password, string FullName, decimal HourlyWage);

public record UpdateEmployeeRequest(string? Username, string? Password, string? FullName, bool IsActive, decimal HourlyWage);
