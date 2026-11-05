namespace Estoka.Application.Common;

/// <summary>Conversões para o fuso de Brasília. Datas são persistidas em UTC (timestamptz).</summary>
public static class BrazilTime
{
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public static DateTimeOffset ToLocal(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, Zone);

    public static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(ToLocal(clock.GetUtcNow()).DateTime);

    /// <summary>Início (00:00 de Brasília) do dia informado, em UTC.</summary>
    public static DateTimeOffset StartOfDayUtc(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, Zone.GetUtcOffset(local)).ToUniversalTime();
    }

    public static DateOnly DateOf(DateTimeOffset value) => DateOnly.FromDateTime(ToLocal(value).DateTime);
}
