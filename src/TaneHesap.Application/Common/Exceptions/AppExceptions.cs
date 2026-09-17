namespace TaneHesap.Application.Common.Exceptions;

/// <summary>İstenen kayıt bulunamadığında fırlatılır (API katmanında 404'e çevrilir).</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key)
        : base($"'{entityName}' ({key}) bulunamadı.")
    {
    }
}

/// <summary>Kullanıcının bu işlem için yetkisi olmadığında fırlatılır (API katmanında 403'e çevrilir).</summary>
public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string? message = null)
        : base(message ?? "Bu işlem için yetkiniz yok.")
    {
    }
}

/// <summary>İş kuralı/doğrulama hatası (API katmanında 400'e çevrilir).</summary>
public class ValidationAppException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationAppException(string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationAppException(IDictionary<string, string[]> errors)
        : base("Bir veya daha fazla doğrulama hatası oluştu.")
    {
        Errors = errors;
    }
}

/// <summary>Çakışma durumu (örn. kullanıcı adı zaten var) — API katmanında 409'a çevrilir.</summary>
public class ConflictAppException : Exception
{
    public ConflictAppException(string message) : base(message)
    {
    }
}
