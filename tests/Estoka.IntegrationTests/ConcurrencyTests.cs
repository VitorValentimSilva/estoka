using Estoka.Application.Common;
using Estoka.Application.Sales;
using Estoka.Application.Security;
using Estoka.Domain.Common;
using Estoka.Domain.Inventory;
using Estoka.Domain.Sales;
using Estoka.Infrastructure.Persistence;
using Estoka.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Estoka.IntegrationTests;

[Collection(AppCollection.Name)]
public class ConcurrencyTests(EstokaFactory factory)
{
    [Fact]
    public async Task Xmin_detecta_alteracao_concorrente_no_produto()
    {
        var productId = await factory.CreateProductAsync(stock: 10);
        var ct = TestContext.Current.CancellationToken;

        await using var scopeA = factory.ScopeAs(Roles.Gerente, "Ana");
        await using var scopeB = factory.ScopeAs(Roles.Gerente, "Beto");
        var dbA = scopeA.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<AppDbContext>();

        var a = await dbA.Products.SingleAsync(p => p.Id == productId, ct);
        var b = await dbB.Products.SingleAsync(p => p.Id == productId, ct);

        dbA.StockMovements.Add(a.RegisterMovement(StockMovementType.Saida, 3, "A", "Ana", DateTimeOffset.UtcNow));
        await dbA.SaveChangesAsync(ct);

        dbB.StockMovements.Add(b.RegisterMovement(StockMovementType.Saida, 3, "B", "Beto", DateTimeOffset.UtcNow));
        var ex = await Should.ThrowAsync<ConcurrencyConflictException>(() => dbB.SaveChangesAsync(ct));
        ex.Message.ShouldContain("alterado por outro usuário");
    }

    [Fact]
    public async Task Dois_pedidos_simultaneos_pelo_ultimo_item_so_um_confirma()
    {
        var ct = TestContext.Current.CancellationToken;
        var productId = await factory.CreateProductAsync(stock: 1);
        var customerId = await factory.CreateCustomerAsync();

        async Task<Guid> Draft()
        {
            await using var scope = factory.ScopeAs(Roles.Vendedor);
            return await scope.ServiceProvider.GetRequiredService<SalesOrderService>().CreateDraftAsync(new SalesOrderInput
            {
                CustomerId = customerId,
                Items = [new SalesOrderItemInput { ProductId = productId, Quantity = 1 }],
            }, ct);
        }

        var orderA = await Draft();
        var orderB = await Draft();

        using var start = new ManualResetEventSlim(false);
        async Task<Exception?> Confirm(Guid id)
        {
            await using var scope = factory.ScopeAs(Roles.Vendedor);
            var service = scope.ServiceProvider.GetRequiredService<SalesOrderService>();
            await Task.Run(start.Wait, ct);
            try
            {
                await service.ConfirmAsync(id, ct);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        var tasks = new[] { Confirm(orderA), Confirm(orderB) };
        start.Set();
        var results = await Task.WhenAll(tasks);

        results.Count(r => r is null).ShouldBe(1, "exatamente um pedido deve ser confirmado");
        var error = results.Single(r => r is not null);
        error.ShouldBeAssignableTo<DomainException>();
        error!.Message.ShouldContain("Estoque insuficiente");

        await using var check = factory.ScopeAs(Roles.Admin);
        var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await db.Products.AsNoTracking().SingleAsync(p => p.Id == productId, ct);
        product.CurrentStock.ShouldBe(0);
        (await db.StockMovements.Where(m => m.ProductId == productId).SumAsync(m => m.Quantity, ct)).ShouldBe(0);
        var statuses = await db.SalesOrders.Where(o => o.Id == orderA || o.Id == orderB).Select(o => o.Status).ToListAsync(ct);
        statuses.ShouldBe([SalesOrderStatus.Confirmado, SalesOrderStatus.Rascunho], ignoreOrder: true);
    }
}
