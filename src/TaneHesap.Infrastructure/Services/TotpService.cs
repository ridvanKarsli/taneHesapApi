using OtpNet;
using TaneHesap.Application.Common.Interfaces;

namespace TaneHesap.Infrastructure.Services;

/// <summary>
/// SUPER_ADMIN/ADMIN authenticator (TOTP) doğrulaması. Otp.NET (OtpNet) paketi kullanılır.
/// bkz. Proje Raporu bölüm 2, 8.
/// </summary>
public class TotpService : ITotpService
{
    public string GenerateSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(key);
    }

    public string GenerateQrCodeUri(string secret, string accountName, string issuer = "taneHesap")
    {
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedAccount = Uri.EscapeDataString(accountName);
        return $"otpauth://totp/{encodedIssuer}:{encodedAccount}?secret={secret}&issuer={encodedIssuer}&digits=6&period=30";
    }

    public bool ValidateCode(string secret, string code)
    {
        var key = Base32Encoding.ToBytes(secret);
        var totp = new Totp(key);
        return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
    }
}
