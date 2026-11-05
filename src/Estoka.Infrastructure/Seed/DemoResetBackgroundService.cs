using Estoka.Application.Common;
using Estoka.Infrastructure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Estoka.Infrastructure.Seed;

/// <summary>
/// Recria os dados de demonstração todos os dias às <see cref="DemoOptions.ResetHour"/>h (Brasília).
/// Hospedagens gratuitas (ex.: Render) "dormem" sem acesso, então o processo pode não estar de pé no horário:
/// ao iniciar, se o último reset registrado for anterior ao horário mais recente, o reset é feito na hora.
/// </summary>
public sealed class DemoResetBackgroundService(
    IServiceScopeFactory scopeFactory, IOptions<DemoOptions> options, TimeProvider clock, ILogger<DemoResetBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var last = await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().GetLastResetAsync(stoppingToken);
            if (IsResetDue(last, clock.GetUtcNow(), options.Value.ResetHour))
            {
                logger.LogInformation("Reset da demonstração atrasado (último: {Last}); executando agora", last);
                await ResetAsync(stoppingToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao verificar o reset pendente da demonstração");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = NextRun(clock.GetUtcNow(), options.Value.ResetHour) - clock.GetUtcNow();
            logger.LogInformation("Próximo reset da demonstração em {Delay}", delay);
            try
            {
                await Task.Delay(delay, clock, stoppingToken);
                await ResetAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao recriar os dados de demonstração");
            }
        }
    }

    private async Task ResetAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentUser>().SetPrincipal(CurrentUser.System());
        await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().ResetAsync(ct);
        var identity = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
        await identity.EnsureDemoUsersAsync(options.Value.Password);
        var removed = await identity.RemoveNonDemoUsersAsync();
        logger.LogInformation("Dados de demonstração recriados ({Removed} usuário(s) criados por visitantes removidos)", removed);
    }

    /// <summary>Próxima ocorrência de <paramref name="hour"/>:00 no horário de Brasília, em UTC.</summary>
    public static DateTimeOffset NextRun(DateTimeOffset nowUtc, int hour)
    {
        var today = BrazilTime.DateOf(nowUtc);
        var candidate = BrazilTime.StartOfDayUtc(today).AddHours(hour);
        return candidate > nowUtc ? candidate : BrazilTime.StartOfDayUtc(today.AddDays(1)).AddHours(hour);
    }

    /// <summary>Ocorrência mais recente (já passada) de <paramref name="hour"/>:00 em Brasília.</summary>
    public static DateTimeOffset PreviousRun(DateTimeOffset nowUtc, int hour) => NextRun(nowUtc, hour).AddDays(-1);

    /// <summary>O reset está pendente se nunca foi feito ou se o último aconteceu antes do horário mais recente.</summary>
    public static bool IsResetDue(DateTimeOffset? lastReset, DateTimeOffset nowUtc, int hour)
        => lastReset is null || lastReset < PreviousRun(nowUtc, hour);
}
