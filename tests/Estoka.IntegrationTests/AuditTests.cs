using System.Text.Json;
using Estoka.Application.Catalog;
using Estoka.Application.Security;
using Estoka.Domain.Auditing;
using Estoka.Infrastructure.Persistence;
using Estoka.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Estoka.IntegrationTests;

[Collection(AppCollection.Name)]
public class AuditTests(EstokaFactory factory)
{
    [Fact]
    public async Task Alteracao_gera_auditoria_com_antes_e_depois_e_preenche_updated_by()
    {
        var ct = TestContext.Current.CancellationToken;
        var productId = await factory.CreateProductAsync(stock: 0, price: 10m);

        await using (var scope = factory.ScopeAs(Roles.Gerente, "Ricardo"))
        {
            var service = scope.ServiceProvider.GetRequiredService<ProductService>();
            var input = ProductInput.From(await service.GetAsync(productId, ct));
            input.SalePrice = 11.9m;
            await service.UpdateAsync(productId, input, ct);
        }

        await using var check = factory.ScopeAs(Roles.Admin);
        var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        var log = await db.AuditLogs.Where(a => a.EntityId == productId.ToString() && a.Action == AuditAction.Alterado)
            .OrderByDescending(a => a.Id).FirstAsync(ct);
        log.EntityName.ShouldBe("Product");
        log.UserName.ShouldBe("Ricardo (Gerente)");
        JsonDocument.Parse(log.OldValues!).RootElement.GetProperty("SalePrice").GetDecimal().ShouldBe(10m);
        JsonDocument.Parse(log.NewValues!).RootElement.GetProperty("SalePrice").GetDecimal().ShouldBe(11.9m);

        var product = await db.Products.AsNoTracking().SingleAsync(p => p.Id == productId, ct);
        product.UpdatedBy.ShouldBe("Ricardo (Gerente)");
        product.CreatedBy.ShouldBe("Teste (Gerente)");
    }

    [Fact]
    public async Task Exclusao_e_logica_e_auditada()
    {
        var ct = TestContext.Current.CancellationToken;
        var productId = await factory.CreateProductAsync(stock: 0);

        await using (var scope = factory.ScopeAs(Roles.Gerente))
            await scope.ServiceProvider.GetRequiredService<ProductService>().DeleteAsync(productId, ct);

        await using var check = factory.ScopeAs(Roles.Admin);
        var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Products.AnyAsync(p => p.Id == productId, ct)).ShouldBeFalse("filtro global oculta excluídos");
        var deleted = await db.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == productId, ct);
        deleted.IsDeleted.ShouldBeTrue();
        deleted.DeletedAt.ShouldNotBeNull();
        (await db.AuditLogs.AnyAsync(a => a.EntityId == productId.ToString() && a.Action == AuditAction.Excluido, ct)).ShouldBeTrue();
    }
}
