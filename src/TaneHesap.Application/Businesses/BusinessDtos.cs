namespace TaneHesap.Application.Businesses;

public record BusinessDto(Guid Id, string Name, string? Address, bool IsActive, DateTime CreatedAtUtc);

public record CreateBusinessRequest(string Name, string? Address);

public record UpdateBusinessRequest(string Name, string? Address, bool IsActive);
