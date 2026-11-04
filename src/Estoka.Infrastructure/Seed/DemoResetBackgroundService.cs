using Estoka.Application.Common;
using Estoka.Infrastructure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Estoka.Infrastructure.Seed;

/// <summary>Recria os dados de demonstração todos os dias às <see cref="DemoOptions.ResetHour"/>h (Brasília).</summary>
public sealed class DemoResetBackgroundService(
    IServiceScopeFactory scopeFactory, IOptions<DemoOptions> options, TimeProvider clock, ILogger<DemoResetBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = NextRun(clock.GetUtcNow(), options.Value.ResetHour) - clock.GetUtcNow();
            logger.LogInformation("Próximo reset da demonstração em {Delay}", delay);
            try
            {
                await Task.Delay(delay, clock, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<CurrentUser>().SetPrincipal(CurrentUser.System());
                await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().ResetAsync(stoppingToken);
                await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().EnsureDemoUsersAsync(options.Value.Password);
                logger.LogInformation("Dados de demonstração recriados");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Falha ao recriar os dados de demonstração");
            }
        }
    }

    /// <summary>Próxima ocorrência de <paramref name="hour"/>:00 no horário de Brasília, em UTC.</summary>
    public static DateTimeOffset NextRun(DateTimeOffset nowUtc, int hour)
    {
        var today = BrazilTime.DateOf(nowUtc);
        var candidate = BrazilTime.StartOfDayUtc(today).AddHours(hour);
        return candidate > nowUtc ? candidate : BrazilTime.StartOfDayUtc(today.AddDays(1)).AddHours(hour);
    }
}
