namespace TaneHesap.Application.Admins;

public record AdminDto(Guid Id, string Username, string FullName, bool IsActive);

public record CreateAdminRequest(string Username, string Password, string FullName);

public record UpdateAdminRequest(string? Username, string? Password, string? FullName, bool IsActive);
