namespace TaneHesap.Application.Employees;

/// <summary>
/// Çalışan cüzdanı: ADMIN günlük çalışma saatini girer (hak ediş = saat × saatlik ücret), çalışana ödeme
/// yapınca cüzdandan düşer. EMPLOYEE yalnızca kendi cüzdanını görür. Kullanıcı hesabı yönetimi
/// (IEmployeeService) ile ayrı tutulur — farklı sorumluluklar (bkz. Proje Raporu bölüm 3.7).
/// </summary>
public interface IEmployeeWalletService
{
    Task<EmployeeWalletDto> GetWalletAsync(Guid businessId, Guid employeeUserId, CancellationToken ct = default);

    Task<EmployeeWorkLogDto> AddWorkLogAsync(Guid businessId, Guid employeeUserId, CreateWorkLogRequest request, Guid userId, CancellationToken ct = default);

    Task DeleteWorkLogAsync(Guid businessId, Guid employeeUserId, Guid workLogId, CancellationToken ct = default);

    /// <summary>Çalışana ödeme: Personnel kategorisindeki otomatik "Personel Ödemesi" gider türüyle bir Expense oluşturur (kasadan/karttan düşer).</summary>
    Task<EmployeePaymentDto> PayAsync(Guid businessId, Guid employeeUserId, CreateEmployeePaymentRequest request, Guid userId, CancellationToken ct = default);
}
