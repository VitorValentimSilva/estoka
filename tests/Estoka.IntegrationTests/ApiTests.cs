using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Estoka.Application.Catalog;
using Estoka.Application.Sales;
using Estoka.Domain.Sales;
using Estoka.IntegrationTests.Infrastructure;
using Estoka.Web.Api;

namespace Estoka.IntegrationTests;

[Collection(AppCollection.Name)]
public class ApiTests(EstokaFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private HttpClient Client(string? key = EstokaFactory.ApiKey)
    {
        var client = factory.CreateClient();
        if (key is not null) client.DefaultRequestHeaders.Add("X-Api-Key", key);
        return client;
    }

    [Fact]
    public async Task Sem_api_key_retorna_401_com_problem_details()
    {
        var response = await Client(null).GetAsync("/api/v1/products", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Api_key_invalida_retorna_401()
        => (await Client("errada").GetAsync("/api/v1/products", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task Lista_produtos_paginada_sem_expor_custo()
    {
        var page = await Client().GetFromJsonAsync<ApiPage<ProductListItem>>("/api/v1/products?pageSize=10&page=2", Json, TestContext.Current.CancellationToken);
        page!.Items.Count.ShouldBe(10);
        page.Page.ShouldBe(2);
        page.TotalItems.ShouldBeGreaterThanOrEqualTo(100);
        page.Items.ShouldAllBe(p => p.CostPrice == null && p.MarginPercent == null);
    }

    [Fact]
    public async Task Produto_inexistente_retorna_404()
    {
        var response = await Client().GetAsync($"/api/v1/products/{Guid.NewGuid()}", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Lista_movimentacoes_e_estoque_baixo()
    {
        var ct = TestContext.Current.CancellationToken;
        (await Client().GetAsync("/api/v1/stock/movements?type=Saida&pageSize=5", ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var low = await Client().GetFromJsonAsync<JsonElement>("/api/v1/stock/low", Json, ct);
        low.GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Cria_e_confirma_pedido_pela_api()
    {
        var ct = TestContext.Current.CancellationToken;
        var productId = await factory.CreateProductAsync(stock: 10, price: 20m);
        var customerId = await factory.CreateCustomerAsync();

        var response = await Client().PostAsJsonAsync("/api/v1/orders",
            new CreateOrderRequest(customerId, PaymentMethod.Boleto, [new CreateOrderItemRequest(productId, 4)], Confirm: true), Json, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location!.OriginalString.ShouldStartWith("/api/v1/orders/");
        var order = await response.Content.ReadFromJsonAsync<SalesOrderDetails>(Json, ct);
        order!.Status.ShouldBe(SalesOrderStatus.Confirmado);
        order.Total.ShouldBe(80m);
        order.SellerName.ShouldBe("Integração (API)");

        var product = await Client().GetFromJsonAsync<ProductDetails>($"/api/v1/products/{productId}", Json, ct);
        product!.CurrentStock.ShouldBe(6);
    }

    [Fact]
    public async Task Pedido_sem_itens_retorna_400_com_erros_de_validacao()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await Client().PostAsJsonAsync("/api/v1/orders",
            new CreateOrderRequest(Guid.NewGuid(), PaymentMethod.Pix, []), Json, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        problem.GetProperty("errors").GetProperty("Items")[0].GetString().ShouldBe("Adicione ao menos um item.");
    }

    [Fact]
    public async Task Estoque_insuficiente_retorna_422()
    {
        var ct = TestContext.Current.CancellationToken;
        var productId = await factory.CreateProductAsync(stock: 1);
        var customerId = await factory.CreateCustomerAsync();
        var response = await Client().PostAsJsonAsync("/api/v1/orders",
            new CreateOrderRequest(customerId, PaymentMethod.Pix, [new CreateOrderItemRequest(productId, 5)], Confirm: true), Json, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync(ct)).ShouldContain("Estoque insuficiente");
    }

    [Fact]
    public async Task Documento_openapi_e_scalar_estao_publicados()
    {
        var ct = TestContext.Current.CancellationToken;
        var doc = await Client(null).GetFromJsonAsync<JsonElement>("/openapi/v1.json", ct);
        doc.GetProperty("paths").TryGetProperty("/api/v1/orders", out _).ShouldBeTrue();
        doc.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("ApiKey", out _).ShouldBeTrue();
        var scalar = await factory.CreateClient().GetAsync("/scalar/", ct);
        scalar.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
