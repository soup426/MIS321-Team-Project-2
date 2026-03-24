namespace MulhollandRealEstate.API.Configuration;

/// <summary>
/// Builds an ADO.NET connection string for Pomelo from config or Heroku-style <c>mysql://</c> URLs
/// (<c>DATABASE_URL</c>, <c>JAWSDB_URL</c>, etc.).
/// </summary>
public static class MysqlConnectionResolver
{
    private static readonly string[] UrlEnvKeys =
    [
        "DATABASE_URL",
        "MYSQL_URL",
        "JAWSDB_URL",
        "CLEARDB_DATABASE_URL"
    ];

    public static string Resolve(IConfiguration configuration)
    {
        var fromConfig = configuration.GetConnectionString("DefaultConnection");
        if (LooksLikeAdoNetMysql(fromConfig))
            return fromConfig!;

        foreach (var key in UrlEnvKeys)
        {
            var raw = Environment.GetEnvironmentVariable(key)
                ?? configuration[key];
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            var trimmed = raw.Trim();
            if (trimmed.StartsWith("mysql://", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("mysql2://", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("mariadb://", StringComparison.OrdinalIgnoreCase))
                return FromMysqlUrl(trimmed, configuration);
        }

        if (!string.IsNullOrWhiteSpace(fromConfig))
            return fromConfig!;

        throw new InvalidOperationException(
            "No MySQL connection found. Set ConnectionStrings:DefaultConnection, or set DATABASE_URL / JAWSDB_URL (mysql://...) on Heroku.");
    }

    private static bool LooksLikeAdoNetMysql(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        var t = s.Trim();
        return t.Contains("Server=", StringComparison.OrdinalIgnoreCase)
               || t.Contains("Host=", StringComparison.OrdinalIgnoreCase);
    }

    private static string FromMysqlUrl(string url, IConfiguration configuration)
    {
        var normalized = url
            .Replace("mysql2://", "mysql://", StringComparison.OrdinalIgnoreCase)
            .Replace("mariadb://", "mysql://", StringComparison.OrdinalIgnoreCase);

        if (!normalized.StartsWith("mysql://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unsupported database URL; expected mysql://...");

        var uri = new Uri(normalized);
        var userInfo = uri.UserInfo;
        string user;
        string password;
        var colon = userInfo.IndexOf(':');
        if (colon < 0)
        {
            user = Uri.UnescapeDataString(userInfo);
            password = "";
        }
        else
        {
            user = Uri.UnescapeDataString(userInfo[..colon]);
            password = Uri.UnescapeDataString(userInfo[(colon + 1)..]);
        }

        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 3306;
        var database = uri.AbsolutePath.Trim('/');
        if (string.IsNullOrEmpty(database))
            throw new InvalidOperationException("Database name missing in URL path.");

        var trustCert = configuration.GetValue("MySql:TrustServerCertificate", true);
        var sslMode = configuration["MySql:SslMode"] ?? "Required";

        return $"Server={host};Port={port};Database={database};User Id={user};Password={password};SslMode={sslMode};TrustServerCertificate={trustCert};";
    }
}
