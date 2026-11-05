using Estoka.Application.Catalog;
using Estoka.Application.Sales;
using Estoka.Application.Security;
using Estoka.Domain.Common;
using Estoka.Domain.Sales;
using Estoka.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Estoka.IntegrationTests;

[Collection(AppCollection.Name)]
public class OrderFlowTests(EstokaFactory factory)
{
    [Fact]
    public async Task Confirmar_baixa_estoque_e_cancelar_devolve()
    {
        var ct = TestContext.Current.CancellationToken;
        var productId = await factory.CreateProductAsync(stock: 20, price: 12.5m);
        var customerId = await factory.CreateCustomerAsync();

        await using var scope = factory.ScopeAs(Roles.Vendedor, "Juliana");
        var orders = scope.ServiceProvider.GetRequiredService<SalesOrderService>();
        var products = scope.ServiceProvider.GetRequiredService<ProductService>();

        var id = await orders.CreateDraftAsync(new SalesOrderInput
        {
            CustomerId = customerId,
            Discount = 5,
            Items = [new SalesOrderItemInput { ProductId = productId, Quantity = 4, UnitPrice = 1m /* ignorado p/ vendedor */ }],
        }, ct);

        var draft = await orders.GetAsync(id, ct);
        draft.Status.ShouldBe(SalesOrderStatus.Rascunho);
        draft.Items.Single().UnitPrice.ShouldBe(12.5m, "vendedor não altera preço de tabela");
        draft.Total.ShouldBe(45m);
        draft.Number.ShouldBeGreaterThan(1000);
        (await products.GetAsync(productId, ct)).CurrentStock.ShouldBe(20, "rascunho não mexe no estoque");

        await orders.ConfirmAsync(id, ct);
        (await products.GetAsync(productId, ct)).CurrentStock.ShouldBe(16);

        await orders.InvoiceAsync(id, ct);
        await orders.CancelAsync(id, "Cliente desistiu", ct);
        (await products.GetAsync(productId, ct)).CurrentStock.ShouldBe(20);
        (await orders.GetAsync(id, ct)).Status.ShouldBe(SalesOrderStatus.Cancelado);
    }

    [Fact]
    public async Task Confirmar_sem_estoque_falha_sem_efeitos_colaterais()
    {
        var ct = TestContext.Current.CancellationToken;
        var productId = await factory.CreateProductAsync(stock: 2);
        var customerId = await factory.CreateCustomerAsync();

        await using var scope = factory.ScopeAs(Roles.Vendedor);
        var orders = scope.ServiceProvider.GetRequiredService<SalesOrderService>();
        var id = await orders.CreateDraftAsync(new SalesOrderInput
        {
            CustomerId = customerId,
            Items = [new SalesOrderItemInput { ProductId = productId, Quantity = 3 }],
        }, ct);

        await Should.ThrowAsync<InsufficientStockException>(() => orders.ConfirmAsync(id, ct));
        (await orders.GetAsync(id, ct)).Status.ShouldBe(SalesOrderStatus.Rascunho);
        (await scope.ServiceProvider.GetRequiredService<ProductService>().GetAsync(productId, ct)).CurrentStock.ShouldBe(2);
    }
}
