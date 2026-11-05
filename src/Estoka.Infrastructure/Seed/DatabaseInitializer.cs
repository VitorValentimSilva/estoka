using Estoka.Infrastructure.Identity;
using Estoka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Estoka.Infrastructure.Seed;

/// <summary>
/// Executado na inicialização: aplica migrations pendentes, garante papéis e, no modo demo,
/// usuários e dados de demonstração. Em um ambiente maior, as migrations seriam um passo
/// separado do pipeline de deploy (ex.: <c>dotnet ef migrations bundle</c>).
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentUser>().SetPrincipal(CurrentUser.System());
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));
        var demo = scope.ServiceProvider.GetRequiredService<IOptions<DemoOptions>>().Value;

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        logger.LogInformation("Verificando migrations pendentes...");
        await db.Database.MigrateAsync(ct);

        var identity = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
        await identity.EnsureRolesAsync();

        if (demo.Enabled)
        {
            await identity.EnsureDemoUsersAsync(demo.Password);
            var seeded = await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedIfEmptyAsync(ct);
            if (seeded) logger.LogInformation("Banco vazio: dados de demonstração criados");
            else if (await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().BackfillDemoBarcodesAsync(ct) is > 0 and var filled)
                logger.LogInformation("Códigos de barras preenchidos em {Count} produto(s) de demonstração", filled);
        }
    }
}
