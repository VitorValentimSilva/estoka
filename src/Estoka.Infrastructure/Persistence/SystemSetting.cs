namespace Estoka.Infrastructure.Persistence;

/// <summary>Chave/valor de uso interno do sistema (ex.: data do último reset da demonstração).</summary>
public sealed class SystemSetting
{
    public const string LastDemoResetKey = "demo.last_reset_utc";

    public required string Key { get; set; }
    public required string Value { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
