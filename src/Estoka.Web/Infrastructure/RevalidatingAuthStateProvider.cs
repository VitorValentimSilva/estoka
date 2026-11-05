using System.Security.Claims;
using Estoka.Infrastructure.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Estoka.Web.Infrastructure;

/// <summary>
/// Revalida periodicamente o usuário de um circuito aberto: se for desativado ou tiver o security stamp
/// alterado (troca de papel, por exemplo), a sessão é encerrada.
/// </summary>
internal sealed class RevalidatingAuthStateProvider(
    ILoggerFactory loggerFactory, IServiceScopeFactory scopeFactory, IOptions<IdentityOptions> options)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var principal = authenticationState.User;
        var user = await userManager.GetUserAsync(principal);
        if (user is null || !user.IsActive) return false;
        if (!userManager.SupportsUserSecurityStamp) return true;
        var stamp = principal.FindFirstValue(options.Value.ClaimsIdentity.SecurityStampClaimType);
        return stamp == await userManager.GetSecurityStampAsync(user);
    }
}
