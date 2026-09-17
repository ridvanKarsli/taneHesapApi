namespace TaneHesap.Application.Common.Interfaces;

/// <summary>
/// SUPER_ADMIN ve ADMIN girişlerinde zorunlu olan authenticator (TOTP tabanlı 2FA) işlemleri.
/// bkz. Proje Raporu bölüm 2, 8.
/// </summary>
public interface ITotpService
{
    /// <summary>Yeni bir kullanıcı için TOTP secret üretir (Google/Microsoft Authenticator ile eşleştirilecek).</summary>
    string GenerateSecret();

    /// <summary>Authenticator uygulamasında taratılacak otpauth:// QR kod URI'si üretir.</summary>
    string GenerateQrCodeUri(string secret, string accountName, string issuer = "taneHesap");

    /// <summary>Kullanıcının authenticator uygulamasında gördüğü 6 haneli kodu doğrular.</summary>
    bool ValidateCode(string secret, string code);
}
