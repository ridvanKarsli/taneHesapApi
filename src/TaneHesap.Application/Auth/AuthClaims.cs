namespace TaneHesap.Application.Auth;

/// <summary>JWT'deki uygulamaya özel claim adları ve bunlara dayanan yetki politikaları.</summary>
public static class AuthClaims
{
    /// <summary>Token'ın rolü Admin ama sahibi bir işletmeye girmiş SÜPER ADMİN (değeri "true").</summary>
    public const string ActingSuperAdmin = "acting_super_admin";
}

public static class AuthPolicies
{
    /// <summary>
    /// Yalnızca bir işletmenin içindeki süper admin: işletme sahibinin göremediği sistem ayarları
    /// (örn. paket servis platformları ve komisyonları).
    /// </summary>
    public const string ActingSuperAdmin = "ActingSuperAdmin";
}
