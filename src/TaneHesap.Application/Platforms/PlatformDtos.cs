namespace TaneHesap.Application.Platforms;

public record PlatformDto(Guid Id, string Name, decimal CommissionPercentage, bool IsActive);

public record CreatePlatformRequest(string Name, decimal CommissionPercentage);

public record UpdatePlatformRequest(string Name, decimal CommissionPercentage, bool IsActive);
