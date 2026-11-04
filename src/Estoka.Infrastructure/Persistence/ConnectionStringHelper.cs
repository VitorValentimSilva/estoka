using Npgsql;

namespace Estoka.Infrastructure.Persistence;

/// <summary>
/// Aceita a connection string no formato .NET (<c>Host=...;Database=...</c>) ou URI
/// (<c>postgresql://user:pass@host:5432/db?sslmode=require</c>, formato do Neon/Render/Heroku).
/// </summary>
public static class ConnectionStringHelper
{
    public static string Normalize(string raw, bool requireSsl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);
        var builder = raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
                      || raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
            ? FromUri(new Uri(raw))
            : new NpgsqlConnectionStringBuilder(raw);

        if (requireSsl && builder.SslMode is SslMode.Disable or SslMode.Allow or SslMode.Prefer)
            builder.SslMode = SslMode.Require;

        return builder.ConnectionString;
    }

    private static NpgsqlConnectionStringBuilder FromUri(Uri uri)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
        };

        var userInfo = uri.UserInfo.Split(':', 2);
        if (userInfo.Length > 0 && userInfo[0].Length > 0) builder.Username = Uri.UnescapeDataString(userInfo[0]);
        if (userInfo.Length > 1) builder.Password = Uri.UnescapeDataString(userInfo[1]);

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(kv[0]).ToLowerInvariant();
            var value = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
            switch (key)
            {
                case "sslmode":
                    builder.SslMode = value.ToLowerInvariant() switch
                    {
                        "disable" => SslMode.Disable,
                        "allow" => SslMode.Allow,
                        "prefer" => SslMode.Prefer,
                        "require" => SslMode.Require,
                        "verify-ca" => SslMode.VerifyCA,
                        "verify-full" => SslMode.VerifyFull,
                        _ => builder.SslMode,
                    };
                    break;
                case "channel_binding":
                    builder.ChannelBinding = value.Equals("require", StringComparison.OrdinalIgnoreCase)
                        ? ChannelBinding.Require
                        : ChannelBinding.Prefer;
                    break;
            }
        }

        return builder;
    }
}
