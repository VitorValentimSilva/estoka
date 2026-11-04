using System.Net;
using Estoka.Infrastructure.Identity;
using Estoka.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Estoka.IntegrationTests;

[Collection(AppCollection.Name)]
public class AuthenticationTests(EstokaFactory factory)
{
    [Fact]
    public async Task Pagina_protegida_redireciona_anonimo_para_login()
    {
        using var client = factory.CreateBrowserClient();
        var response = await client.GetAsync("/produtos", TestContext.Current.CancellationToken);
        response.IsRedirectTo("/login").ShouldBeTrue($"esperado redirect para /login, veio {response.StatusCode} {response.Headers.Location}");
    }

    [Fact]
    public async Task Tela_de_login_mostra_acesso_de_demonstracao()
    {
        using var client = factory.CreateBrowserClient();
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/login", TestContext.Current.CancellationToken));
        html.ShouldContain("Acesso de demonstração");
        html.ShouldContain("vendedor@estoka.app");
        html.ShouldContain("Demo@1234");
    }

    [Theory]
    [InlineData("admin@estoka.app")]
    [InlineData("gerente@estoka.app")]
    [InlineData("vendedor@estoka.app")]
    public async Task Login_dos_usuarios_de_demo_funciona(string email)
    {
        using var client = factory.CreateBrowserClient();
        var response = await client.LoginAsync(email);
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);
        response.Headers.GetValues("Set-Cookie").ShouldContain(c => c.StartsWith("estoka.auth="));

        var page = await client.GetAsync("/produtos", TestContext.Current.CancellationToken);
        page.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Senha_errada_nao_autentica()
    {
        using var client = factory.CreateBrowserClient();
        var response = await client.LoginAsync("admin@estoka.app", "errada");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("E-mail ou senha inválidos");
    }

    [Fact]
    public async Task Usuario_desativado_nao_consegue_entrar()
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = new AppUser { UserName = "inativo@estoka.app", Email = "inativo@estoka.app", FullName = "Inativo", IsActive = false };
            (await users.CreateAsync(user, "Demo@1234")).Succeeded.ShouldBeTrue();
        }

        using var client = factory.CreateBrowserClient();
        var response = await client.LoginAsync("inativo@estoka.app");
        WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("Usuário desativado");
    }

    [Fact]
    public async Task Health_check_responde_com_banco_saudavel()
    {
        using var client = factory.CreateBrowserClient();
        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("\"database\":{\"status\":\"Healthy\"");
    }
}
