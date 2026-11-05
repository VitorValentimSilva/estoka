using Estoka.Infrastructure.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Estoka.Web.Infrastructure;

/// <summary>
/// No Blazor Server não há HttpContext durante o circuito: este handler mantém o <see cref="CurrentUser"/>
/// do escopo do circuito sincronizado com o <see cref="AuthenticationStateProvider"/>.
/// </summary>
internal sealed class CurrentUserCircuitHandler(AuthenticationStateProvider authenticationStateProvider, CurrentUser currentUser)
    : CircuitHandler, IDisposable
{
    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        authenticationStateProvider.AuthenticationStateChanged += OnAuthenticationChanged;
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        currentUser.SetPrincipal(state.User);
    }

    public override async Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        currentUser.SetPrincipal(state.User);
    }

    private void OnAuthenticationChanged(Task<AuthenticationState> task)
    {
        _ = Update();

        async Task Update()
        {
            var state = await task;
            currentUser.SetPrincipal(state.User);
        }
    }

    public void Dispose() => authenticationStateProvider.AuthenticationStateChanged -= OnAuthenticationChanged;
}
