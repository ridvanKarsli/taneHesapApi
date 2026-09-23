namespace TaneHesap.Application.Activity;

public interface IActivityService
{
    Task<List<ActivityEntryDto>> GetAsync(Guid businessId, ActivityQuery query, CancellationToken ct = default);

    /// <summary>Filtre listesi için işletmedeki kullanıcılar (ADMIN + EMPLOYEE).</summary>
    Task<List<ActivityUserDto>> GetUsersAsync(Guid businessId, CancellationToken ct = default);

    /// <summary>Filtre listesi için izlenen kayıt türleri ve Türkçe adları.</summary>
    IReadOnlyList<ActivityKindDto> GetKinds();
}
