namespace TaneHesap.Application.Employees;

/// <summary>
/// ADMIN'in kendi işletmesine çalışan (EMPLOYEE) ekleyip yönetmesi.
/// Ad soyad, kullanıcı adı, şifre — tamamı ADMIN tarafından belirlenir; EMPLOYEE girişinde 2FA yoktur.
/// bkz. Proje Raporu bölüm 3.7.
/// </summary>
public interface IEmployeeService
{
    Task<List<EmployeeDto>> GetAllAsync(Guid businessId, CancellationToken ct = default);

    Task<EmployeeDto> CreateAsync(Guid businessId, CreateEmployeeRequest request, CancellationToken ct = default);

    /// <summary>Çalışan hesabını siler (girdiği giderler geçmiş olarak kalır).</summary>
    Task DeleteAsync(Guid businessId, Guid employeeId, CancellationToken ct = default);

    Task<EmployeeDto> UpdateAsync(Guid businessId, Guid employeeId, UpdateEmployeeRequest request, CancellationToken ct = default);
}
