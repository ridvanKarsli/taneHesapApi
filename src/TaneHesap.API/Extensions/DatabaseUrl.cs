using Npgsql;

namespace TaneHesap.API.Extensions;

/// <summary>
/// Railway gibi platformların verdiği <c>DATABASE_URL</c> (postgresql://kullanici:sifre@host:port/db)
/// değerini Npgsql bağlantı dizesine çevirir. Tanımlıysa appsettings'teki bağlantı dizesinin yerine geçer.
/// </summary>
public static class DatabaseUrl
{
    public static void ApplyIfPresent(ConfigurationManager configuration)
    {
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (string.IsNullOrWhiteSpace(databaseUrl))
        {
            return;
        }

        configuration["ConnectionStrings:DefaultConnection"] = ToNpgsqlConnectionString(databaseUrl);
    }

    public static string ToNpgsqlConnectionString(string databaseUrl)
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
            SslMode = SslMode.Prefer,
        };

        return builder.ConnectionString;
    }
}
