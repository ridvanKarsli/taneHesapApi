namespace TaneHesap.Application.Employees;

public record EmployeeDto(Guid Id, string Username, string FullName, bool IsActive, decimal HourlyWage);

public record CreateEmployeeRequest(string Username, string Password, string FullName, decimal HourlyWage);

public record UpdateEmployeeRequest(string? Username, string? Password, string? FullName, bool IsActive, decimal HourlyWage);
