using Estoka.Infrastructure.Seed;

namespace Estoka.UnitTests.Infrastructure;

public class DemoResetScheduleTests
{
    [Fact]
    public void Proximo_reset_e_4h_de_brasilia_do_mesmo_dia_se_ainda_nao_passou()
    {
        // 02:00 em Brasília (UTC-3) = 05:00 UTC
        var now = new DateTimeOffset(2026, 11, 4, 5, 0, 0, TimeSpan.Zero);
        DemoResetBackgroundService.NextRun(now, 4).ShouldBe(new DateTimeOffset(2026, 11, 4, 7, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Proximo_reset_e_no_dia_seguinte_se_ja_passou()
    {
        var now = new DateTimeOffset(2026, 11, 4, 15, 0, 0, TimeSpan.Zero);
        DemoResetBackgroundService.NextRun(now, 4).ShouldBe(new DateTimeOffset(2026, 11, 5, 7, 0, 0, TimeSpan.Zero));
    }
}

public class DemoResetDueTests
{
    // 4h em Brasília = 07:00 UTC
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Nunca_resetado_esta_pendente() => DemoResetBackgroundService.IsResetDue(null, Now, 4).ShouldBeTrue();

    [Fact]
    public void Resetado_depois_das_4h_de_hoje_nao_esta_pendente()
        => DemoResetBackgroundService.IsResetDue(new DateTimeOffset(2026, 11, 5, 7, 5, 0, TimeSpan.Zero), Now, 4).ShouldBeFalse();

    [Fact]
    public void Servico_dormiu_durante_o_horario_do_reset_fica_pendente()
        => DemoResetBackgroundService.IsResetDue(new DateTimeOffset(2026, 11, 4, 20, 0, 0, TimeSpan.Zero), Now, 4).ShouldBeTrue();
}
