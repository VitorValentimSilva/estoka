using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Estoka.IntegrationTests.Infrastructure;

public static partial class HttpLogin
{
    public static HttpClient CreateBrowserClient(this EstokaFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    /// <summary>Faz login pelo formulário real (SSR + antiforgery). Retorna a resposta do POST.</summary>
    public static async Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password = "Demo@1234")
    {
        var page = await client.GetStringAsync("/login");
        var token = TokenRegex().Match(page).Groups[1].Value;
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "login",
            ["__RequestVerificationToken"] = token,
            ["Input.Email"] = email,
            ["Input.Password"] = password,
        });
        return await client.PostAsync("/login", form);
    }

    public static bool IsRedirectTo(this HttpResponseMessage response, string pathStart)
        => response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.SeeOther
           && response.Headers.Location?.OriginalString.Contains(pathStart, StringComparison.OrdinalIgnoreCase) == true;

    [GeneratedRegex("name=\"__RequestVerificationToken\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();
}
