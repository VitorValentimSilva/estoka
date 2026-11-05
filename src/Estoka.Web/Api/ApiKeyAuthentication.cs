using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Estoka.Application.Security;
using Estoka.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Estoka.Web.Api;

public sealed class ApiKeyAuthOptions : AuthenticationSchemeOptions
{
    public const string Scheme = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    /// <summary>Chave aceita (configuração <c>Api:Key</c>). Vazia desabilita a API.</summary>
    public string? Key { get; set; }
}

/// <summary>Autenticação simples por chave no cabeçalho <c>X-Api-Key</c> — suficiente para a demonstração/integrações internas.</summary>
public sealed class ApiKeyAuthenticationHandler(IOptionsMonitor<ApiKeyAuthOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<ApiKeyAuthOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyAuthOptions.HeaderName, out var provided) || string.IsNullOrEmpty(provided))
            return Task.FromResult(AuthenticateResult.NoResult());
        if (string.IsNullOrEmpty(Options.Key) || !FixedTimeEquals(provided.ToString(), Options.Key))
            return Task.FromResult(AuthenticateResult.Fail("API key inválida."));

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "api"),
            new Claim(ClaimTypes.Name, "Integração (API)"),
            new Claim(CurrentUser.FullNameClaim, "Integração (API)"),
            new Claim(ClaimTypes.Role, Roles.Api),
        ], ApiKeyAuthOptions.Scheme);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), ApiKeyAuthOptions.Scheme)));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = $"{ApiKeyAuthOptions.Scheme} header=\"{ApiKeyAuthOptions.HeaderName}\"";
        await Context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = Context,
            ProblemDetails = { Status = 401, Title = "Não autenticado", Detail = $"Informe uma API key válida no cabeçalho {ApiKeyAuthOptions.HeaderName}." },
        });
    }

    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        await Context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = Context, ProblemDetails = { Status = 403, Title = "Acesso negado" },
        });
    }

    private static bool FixedTimeEquals(string a, string b)
        => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
