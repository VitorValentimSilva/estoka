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
