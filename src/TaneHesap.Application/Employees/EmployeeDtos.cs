namespace TaneHesap.Application.Employees;

public record EmployeeDto(Guid Id, string Username, string FullName, bool IsActive);

public record CreateEmployeeRequest(string Username, string Password, string FullName);

public record UpdateEmployeeRequest(string? Username, string? Password, string? FullName, bool IsActive);
