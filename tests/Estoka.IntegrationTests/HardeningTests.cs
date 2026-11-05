using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Estoka.Application.Catalog;
using Estoka.Application.Inventory;
using Estoka.Application.Sales;
using Estoka.Application.Security;
using Estoka.Application.Users;
using Estoka.Domain.Common;
using Estoka.Domain.Sales;
using Estoka.Infrastructure.Seed;
using Estoka.IntegrationTests.Infrastructure;
using Estoka.Web.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Estoka.IntegrationTests;

/// <summary>Correções da revisão de bugs/segurança (rodada 1).</summary>
[Collection(AppCollection.Name)]
public class HardeningTests(EstokaFactory factory)
{
    private static SalesOrderInput Order(Guid customerId, Guid productId, decimal qty, decimal itemDiscount = 0, decimal orderDiscount = 0) => new()
    {
        CustomerId = customerId,
        Discount = orderDiscount,
        Items = [new SalesOrderItemInput { ProductId = productId, Quantity = qty, Discount = itemDiscount }],
    };

    [Fact]
    public async Task Vendedor_concede_ate_10_por_cento_de_desconto()
    {
        var ct = TestContext.Current.CancellationToken;
        var productId = await factory.CreateProductAsync(stock: 100, price: 10m);
        var customerId = await factory.CreateCustomerAsync();
        await using var scope = factory.ScopeAs(Roles.Vendedor);
        var orders = scope.ServiceProvider.GetRequiredService<SalesOrderService>();

        // bruto R$ 100: 6 no item + 4 no pedido = 10% → permitido
        var ok = await orders.CreateDraftAsync(Order(customerId, productId, 10, itemDiscount: 6, orderDiscount: 4), ct);
        (await orders.GetAsync(ok, ct)).Total.ShouldBe(90m);

        var ex = await Should.ThrowAsync<DomainException>(() => orders.CreateDraftAsync(Order(customerId, productId, 10, orderDiscount: 15), ct));
        ex.Message.ShouldContain("acima do permitido");
    }

    [Fact]
    public async Task Gerente_concede_desconto_acima_do_limite_do_vendedor()
    {
        var ct = TestContext.Current.CancellationToken;
        var productId = await factory.CreateProductAsync(stock: 100, price: 10m);
        var customerId = await factory.CreateCustomerAsync();
        await using var scope = factory.ScopeAs(Roles.Gerente);
        var id = await scope.ServiceProvider.GetRequiredService<SalesOrderService>()
            .CreateDraftAsync(Order(customerId, productId, 10, orderDiscount: 30), ct);
        id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Api_respeita_o_limite_de_desconto()
    {
        var ct = TestContext.Current.CancellationToken;
        var productId = await factory.CreateProductAsync(stock: 100, price: 10m);
        var customerId = await factory.CreateCustomerAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", EstokaFactory.ApiKey);
        var response = await client.PostAsJsonAsync("/api/v1/orders",
            new CreateOrderRequest(customerId, PaymentMethod.Pix, [new CreateOrderItemRequest(productId, 10)], Discount: 50), ct);
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Contas_de_demonstracao_sao_protegidas()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = factory.ScopeAs(Roles.Admin, "Visitante");
        var users = scope.ServiceProvider.GetRequiredService<IUserAdminService>();
        var list = await users.ListAsync(ct);
        var seller = list.Single(u => u.Email == "vendedor@estoka.app");
        seller.IsProtected.ShouldBeTrue();

        await Should.ThrowAsync<DomainException>(() => users.SetActiveAsync(seller.Id, false, ct));
        await Should.ThrowAsync<DomainException>(() => users.ChangeRoleAsync(seller.Id, Roles.Admin, ct));
        (await users.ListAsync(ct)).Single(u => u.Id == seller.Id).IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Reset_remove_usuarios_criados_por_visitantes_e_mantem_os_de_demo()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = factory.ScopeAs(Roles.Admin);
        var users = scope.ServiceProvider.GetRequiredService<IUserAdminService>();
        await users.CreateAsync(new CreateUserInput { FullName = "Visitante", Email = $"v{Guid.NewGuid():N}"[..10] + "@x.com", Role = Roles.Vendedor, Password = "Senha@123" }, ct);

        var removed = await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().RemoveNonDemoUsersAsync();

        removed.ShouldBeGreaterThan(0);
        var remaining = await users.ListAsync(ct);
        remaining.Select(u => u.Email).ShouldBe(DemoUsers.All.Select(u => u.Email), ignoreOrder: true);
    }

    [Fact]
    public async Task Seed_registra_a_data_do_ultimo_reset()
    {
        await using var scope = factory.ScopeAs(Roles.Admin);
        var last = await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().GetLastResetAsync(TestContext.Current.CancellationToken);
        last.ShouldNotBeNull();
        DemoResetBackgroundService.IsResetDue(last, DateTimeOffset.UtcNow, 4).ShouldBeFalse();
    }

    [Fact]
    public async Task Entrada_de_estoque_encontra_produto_inativo_mas_a_venda_nao()
    {
        var ct = TestContext.Current.CancellationToken;
        var productId = await factory.CreateProductAsync(stock: 0);
        await using var scope = factory.ScopeAs(Roles.Gerente);
        var products = scope.ServiceProvider.GetRequiredService<ProductService>();
        var input = ProductInput.From(await products.GetAsync(productId, ct));
        input.IsActive = false;
        await products.UpdateAsync(productId, input, ct);

        (await products.SearchForStockAsync(input.Sku, 5, ct)).ShouldContain(p => p.Id == productId && !p.IsActive);
        (await products.SearchForSaleAsync(input.Sku, 5, ct)).ShouldNotContain(p => p.Id == productId);

        await scope.ServiceProvider.GetRequiredService<StockService>()
            .RegisterEntryAsync(new StockEntryInput { ProductId = productId, Quantity = 5, Reason = "Reposição" }, ct);
        (await products.GetAsync(productId, ct)).CurrentStock.ShouldBe(5);
    }

    [Fact]
    public async Task Respostas_tem_cabecalhos_de_seguranca()
    {
        using var client = factory.CreateBrowserClient();
        var response = await client.GetAsync("/login", TestContext.Current.CancellationToken);
        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        response.Headers.GetValues("Referrer-Policy").ShouldNotBeEmpty();
        var csp = string.Join(";", response.Headers.GetValues("Content-Security-Policy"));
        csp.ShouldContain("default-src 'self'");
        csp.ShouldContain("frame-ancestors 'self'");
    }

    [Fact]
    public async Task Health_nao_expoe_detalhes_de_erro_fora_de_desenvolvimento()
    {
        using var client = factory.CreateBrowserClient();
        var json = await client.GetFromJsonAsync<JsonElement>("/health", TestContext.Current.CancellationToken);
        json.GetProperty("checks").GetProperty("database").GetProperty("error").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Api_nao_expoe_custo_em_detalhe_de_produto_nem_em_movimentacoes()
    {
        var ct = TestContext.Current.CancellationToken;
        var productId = await factory.CreateProductAsync(stock: 3, cost: 7m);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", EstokaFactory.ApiKey);

        var product = await client.GetFromJsonAsync<JsonElement>($"/api/v1/products/{productId}", ct);
        product.GetProperty("costPrice").ValueKind.ShouldBe(JsonValueKind.Null);
        product.GetProperty("marginPercent").ValueKind.ShouldBe(JsonValueKind.Null);

        var movements = await client.GetFromJsonAsync<JsonElement>($"/api/v1/stock/movements?productId={productId}", ct);
        movements.GetProperty("items").EnumerateArray().ShouldAllBe(m => m.GetProperty("unitCost").ValueKind == JsonValueKind.Null);
    }
}

/// <summary>Factory com limite baixo de login para validar o 429 (banco próprio, isolado dos demais testes).</summary>
public sealed class LowRateLimitFactory : EstokaFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("RateLimiting:LoginPerMinute", "3");
        builder.UseSetting("RateLimiting:ApiPerMinute", "5");
    }
}

public class RateLimitTests(LowRateLimitFactory factory) : IClassFixture<LowRateLimitFactory>
{
    [Fact]
    public async Task Login_e_api_sao_limitados_por_ip()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateBrowserClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
            statuses.Add((await client.LoginAsync("admin@estoka.app", "senha-errada")).StatusCode);
        statuses.Take(3).ShouldAllBe(s => s == HttpStatusCode.OK);
        statuses[3].ShouldBe(HttpStatusCode.TooManyRequests);

        using var api = factory.CreateClient();
        api.DefaultRequestHeaders.Add("X-Api-Key", EstokaFactory.ApiKey);
        HttpResponseMessage last = null!;
        for (var i = 0; i < 6; i++) last = await api.GetAsync("/api/v1/stock/low", ct);
        last.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        last.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }
}
