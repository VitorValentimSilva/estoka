namespace Estoka.Infrastructure.Seed;

public sealed class DemoOptions
{
    public const string Section = "Demo";

    /// <summary>Liga o modo demonstração: seed automático, usuários de demo e reset diário.</summary>
    public bool Enabled { get; set; }

    /// <summary>Hora (horário de Brasília) em que os dados de demonstração são recriados.</summary>
    public int ResetHour { get; set; } = 4;

    public string Password { get; set; } = "Demo@1234";
}

public static class DemoUsers
{
    public static readonly (string Email, string FullName, string Role)[] All =
    [
        ("admin@estoka.app", "Mariana Alves", Application.Security.Roles.Admin),
        ("gerente@estoka.app", "Ricardo Lima", Application.Security.Roles.Gerente),
        ("vendedor@estoka.app", "Juliana Costa", Application.Security.Roles.Vendedor),
    ];
}
