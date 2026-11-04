using System.Security.Claims;
using Estoka.Application.Security;

namespace Estoka.Infrastructure.Identity;

/// <summary>
/// Usuário corrente do escopo. A camada Web alimenta o principal tanto em requisições HTTP
/// (middleware) quanto em circuitos do Blazor Server (CircuitHandler).
/// </summary>
public sealed class CurrentUser : ICurrentUser
{
    public const string FullNameClaim = "estoka:full_name";
    private ClaimsPrincipal _principal = new(new ClaimsIdentity());

    public ClaimsPrincipal Principal => _principal;

    public void SetPrincipal(ClaimsPrincipal? principal) => _principal = principal ?? new ClaimsPrincipal(new ClaimsIdentity());

    public string? UserId => _principal.FindFirstValue(ClaimTypes.NameIdentifier);

    public string UserName =>
        _principal.FindFirstValue(FullNameClaim)
        ?? _principal.Identity?.Name
        ?? "Sistema";

    public bool IsAuthenticated => _principal.Identity?.IsAuthenticated == true;

    public IReadOnlyCollection<string> Roles =>
        _principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();

    /// <summary>Principal técnico para tarefas em segundo plano (seed, reset da demo).</summary>
    public static ClaimsPrincipal System(string name = "Sistema") => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, "system"),
        new Claim(ClaimTypes.Name, name),
        new Claim(FullNameClaim, name),
        new Claim(ClaimTypes.Role, Application.Security.Roles.Admin),
    ], "system"));
}
