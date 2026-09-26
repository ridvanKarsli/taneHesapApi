namespace TaneHesap.Application.Auth;

public record ServiceResult<T>(bool Success, T? Data, string? ErrorMessage)
{
    public static ServiceResult<T> Ok(T data) => new(true, data, null);
    public static ServiceResult<T> Fail(string errorMessage) => new(false, default, errorMessage);
}

public interface IAuthService
{
    /// <summary>
    /// Kullanıcı adı/şifre (+ SUPER_ADMIN/ADMIN için TOTP kodu) doğrular, başarılıysa
    /// access + refresh token üretir. bkz. Proje Raporu bölüm 5 (iş akışı 7-8).
    /// </summary>
    Task<ServiceResult<LoginResponse>> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Geçerli bir refresh token ile yeni bir access + refresh token çifti üretir (rotation).
    /// bkz. Proje Raporu bölüm 8 (JWT token yenileme).
    /// </summary>
    Task<ServiceResult<LoginResponse>> RefreshTokenAsync(string refreshToken, string? ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Geçerli refresh token ile yeni çift üretir; <paramref name="actingBusinessId"/> verildiyse ve kullanıcı SUPER_ADMIN ise
    /// oturum o işletmenin sahibi (Admin) yetkisiyle sürer (işletme içi görünüm kalıcıdır).
    /// </summary>
    Task<ServiceResult<LoginResponse>> RefreshTokenAsync(string refreshToken, Guid? actingBusinessId, string? ipAddress, CancellationToken ct = default);

    /// <summary>
    /// SUPER_ADMIN bir işletmeye girer: aynı kullanıcı kimliğiyle (denetim kaydında kendisi görünür) ama
    /// Admin rolü ve o işletmenin business_id'siyle token alır; kiracı filtresi o işletmeye kilitlenir.
    /// </summary>
    Task<ServiceResult<LoginResponse>> EnterBusinessAsync(Guid superAdminUserId, Guid businessId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Çıkış / "tüm cihazlardan çıkış yap" senaryosunda refresh token'ı iptal eder.</summary>
    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default);

}
