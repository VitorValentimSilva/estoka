using Estoka.Application.Security;
using Estoka.Domain.Common;
using Estoka.Infrastructure.Persistence;
using Estoka.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Estoka.IntegrationTests;

[Collection(AppCollection.Name)]
public class SeedTests(EstokaFactory factory)
{
    [Fact]
    public async Task Seed_gera_volume_realista()
    {
        await using var scope = factory.ScopeAs(Roles.Admin);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ct = TestContext.Current.CancellationToken;

        (await db.Categories.CountAsync(ct)).ShouldBe(8);
        (await db.Suppliers.CountAsync(ct)).ShouldBe(10);
        (await db.Products.CountAsync(p => p.Sku.Length == 7, ct)).ShouldBe(120);
        (await db.Customers.CountAsync(c => c.Name != "Cliente de Teste", ct)).ShouldBe(60);
        (await db.SalesOrders.CountAsync(ct)).ShouldBeGreaterThanOrEqualTo(200);
        (await db.Products.CountAsync(p => p.IsActive && p.CurrentStock < p.MinimumStock, ct)).ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Documentos_dos_clientes_sao_validos()
    {
        await using var scope = factory.ScopeAs(Roles.Admin);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var customers = await db.Customers.ToListAsync(TestContext.Current.CancellationToken);
        customers.ShouldAllBe(c => c.Type == Domain.Customers.PersonType.PF
            ? DocumentValidator.IsValidCpf(c.Document)
            : DocumentValidator.IsValidCnpj(c.Document));
    }

    [Fact]
    public async Task Estoque_atual_e_igual_a_soma_das_movimentacoes()
    {
        await using var scope = factory.ScopeAs(Roles.Admin);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ct = TestContext.Current.CancellationToken;

        var inconsistent = await db.Products.IgnoreQueryFilters()
            .Select(p => new { p.Sku, p.CurrentStock, Sum = db.StockMovements.Where(m => m.ProductId == p.Id).Sum(m => (decimal?)m.Quantity) ?? 0 })
            .Where(x => x.CurrentStock != x.Sum)
            .ToListAsync(ct);

        inconsistent.ShouldBeEmpty();
        (await db.Products.AnyAsync(p => p.CurrentStock < 0, ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Pedidos_confirmados_tem_movimentacoes_de_saida_e_numeracao_legivel()
    {
        await using var scope = factory.ScopeAs(Roles.Admin);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ct = TestContext.Current.CancellationToken;

        var delivered = await db.SalesOrders.Where(o => o.Status == Domain.Sales.SalesOrderStatus.Entregue).OrderBy(o => o.Number).Take(5).ToListAsync(ct);
        foreach (var order in delivered)
        {
            var movements = await db.StockMovements.Where(m => m.SalesOrderId == order.Id).ToListAsync(ct);
            movements.ShouldNotBeEmpty();
            movements.ShouldAllBe(m => m.Reason == $"Venda PED-{order.Number:D6}");
        }
        (await db.SalesOrders.MinAsync(o => o.Number, ct)).ShouldBe(1001);
    }
}
