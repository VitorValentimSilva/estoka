using Estoka.Infrastructure.Persistence;
using Npgsql;

namespace Estoka.UnitTests.Infrastructure;

public class ConnectionStringHelperTests
{
    [Fact]
    public void Converte_uri_do_neon()
    {
        var cs = ConnectionStringHelper.Normalize(
            "postgresql://neondb_owner:s3nh%40forte@ep-cool-123.sa-east-1.aws.neon.tech/neondb?sslmode=require&channel_binding=require",
            requireSsl: true);
        var b = new NpgsqlConnectionStringBuilder(cs);

        b.Host.ShouldBe("ep-cool-123.sa-east-1.aws.neon.tech");
        b.Port.ShouldBe(5432);
        b.Database.ShouldBe("neondb");
        b.Username.ShouldBe("neondb_owner");
        b.Password.ShouldBe("s3nh@forte");
        b.SslMode.ShouldBe(SslMode.Require);
        b.ChannelBinding.ShouldBe(ChannelBinding.Require);
    }

    [Fact]
    public void Converte_uri_postgres_com_porta()
    {
        var b = new NpgsqlConnectionStringBuilder(ConnectionStringHelper.Normalize("postgres://u:p@db:6543/app", requireSsl: false));
        b.Port.ShouldBe(6543);
        b.Database.ShouldBe("app");
    }

    [Fact]
    public void Mantem_formato_dotnet_e_exige_ssl_em_producao()
    {
        var b = new NpgsqlConnectionStringBuilder(
            ConnectionStringHelper.Normalize("Host=localhost;Database=estoka;Username=u;Password=p", requireSsl: true));
        b.Host.ShouldBe("localhost");
        b.SslMode.ShouldBe(SslMode.Require);
    }

    [Fact]
    public void Nao_forca_ssl_quando_desabilitado()
    {
        var b = new NpgsqlConnectionStringBuilder(
            ConnectionStringHelper.Normalize("Host=localhost;Database=estoka;Username=u;Password=p", requireSsl: false));
        b.SslMode.ShouldNotBe(SslMode.Require);
    }
}
