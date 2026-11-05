using System.Security.Claims;
using Estoka.Application.Security;
using Estoka.Infrastructure.Identity;
using Estoka.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Estoka.IntegrationTests.Infrastructure;

/// <summary>
/// Sobe a aplicação real contra um banco PostgreSQL exclusivo (criado e removido a cada execução).
/// Configure <c>ESTOKA_TEST_PG</c> para apontar para outro servidor (padrão: Postgres local).
/// </summary>
public class EstokaFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly string ServerConnection =
        Environment.GetEnvironmentVariable("ESTOKA_TEST_PG") ?? "Host=localhost;Username=estoka;Password=estoka;Database=postgres";

    private readonly string _database = $"estoka_test_{Guid.NewGuid():N}"[..30];

    public string ConnectionString => new NpgsqlConnectionStringBuilder(ServerConnection) { Database = _database, Pooling = true }.ConnectionString;

    public const string ApiKey = "test-api-key";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        builder.UseSetting("Database:RequireSsl", "false");
        builder.UseSetting("Demo:Enabled", "true");
        builder.UseSetting("Api:Key", ApiKey);
        // Os testes fazem dezenas de logins em segundos: limites altos aqui; o 429 é testado numa factory própria.
        builder.UseSetting("RateLimiting:LoginPerMinute", "100000");
        builder.UseSetting("RateLimiting:ApiPerMinute", "100000");
    }

    public async ValueTask InitializeAsync()
    {
        // Força a inicialização do host (migrations + seed) antes dos testes.
        _ = Services;
        await Task.CompletedTask;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var conn = new NpgsqlConnection(ServerConnection);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Cria um escopo de DI autenticado com o papel informado (simula o usuário logado).</summary>
    public AsyncServiceScope ScopeAs(string role, string name = "Teste")
    {
        var scope = Services.CreateAsyncScope();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, name),
            new Claim(CurrentUser.FullNameClaim, $"{name} ({role})"),
            new Claim(ClaimTypes.Role, role),
        ], "test"));
        scope.ServiceProvider.GetRequiredService<CurrentUser>().SetPrincipal(principal);
        return scope;
    }
}

[CollectionDefinition(Name)]
public sealed class AppCollection : ICollectionFixture<EstokaFactory>
{
    public const string Name = "app";
}
