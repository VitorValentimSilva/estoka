using Estoka.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Estoka.Web.Endpoints;

internal static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        // Logout via POST (com antiforgery) para não ser disparado por links/imagens de terceiros.
        app.MapPost("/conta/sair", async ([FromServices] SignInManager<AppUser> signInManager, [FromForm] string? returnUrl) =>
        {
            await signInManager.SignOutAsync();
            return TypedResults.LocalRedirect("/login?saiu=1");
        }).RequireAuthorization();
    }
}
