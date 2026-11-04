using Estoka.Application.Catalog;
using Estoka.Application.Common;
using Estoka.Application.Security;
using Estoka.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Estoka.IntegrationTests;

/// <summary>Autorização aplicada nos serviços — independente da tela.</summary>
[Collection(AppCollection.Name)]
public class AuthorizationTests(EstokaFactory factory)
{
    [Fact]
    public async Task Vendedor_nao_recebe_custo_nem_margem()
    {
        await using var scope = factory.ScopeAs(Roles.Vendedor);
        var service = scope.ServiceProvider.GetRequiredService<ProductService>();
        var page = await service.GetPagedAsync(new ProductQuery { PageSize = 50 }, TestContext.Current.CancellationToken);

        page.Items.ShouldNotBeEmpty();
        page.Items.ShouldAllBe(p => p.CostPrice == null && p.MarginPercent == null);
        page.Items.ShouldAllBe(p => p.SalePrice > 0);

        var details = await service.GetAsync(page.Items[0].Id, TestContext.Current.CancellationToken);
        details.CostPrice.ShouldBeNull();
        details.MarginPercent.ShouldBeNull();
    }

    [Fact]
    public async Task Vendedor_tentando_ver_custo_recebe_forbidden()
    {
        await using var scope = factory.ScopeAs(Roles.Vendedor);
        var service = scope.ServiceProvider.GetRequiredService<ProductService>();
        var any = (await service.GetPagedAsync(new ProductQuery { PageSize = 1 }, TestContext.Current.CancellationToken)).Items[0];

        await Should.ThrowAsync<ForbiddenException>(() => service.GetCostAsync(any.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Vendedor_nao_cadastra_produto()
    {
        await using var scope = factory.ScopeAs(Roles.Vendedor);
        await Should.ThrowAsync<ForbiddenException>(() => scope.ServiceProvider.GetRequiredService<ProductService>()
            .CreateAsync(new ProductInput { Sku = "X", Name = "X", CategoryId = Guid.NewGuid(), SalePrice = 1 }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Gerente_ve_custo_e_margem()
    {
        await using var scope = factory.ScopeAs(Roles.Gerente);
        var page = await scope.ServiceProvider.GetRequiredService<ProductService>()
            .GetPagedAsync(new ProductQuery { PageSize = 20 }, TestContext.Current.CancellationToken);
        page.Items.ShouldAllBe(p => p.CostPrice != null && p.MarginPercent != null);
    }

    [Fact]
    public async Task Usuario_anonimo_e_bloqueado()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await Should.ThrowAsync<ForbiddenException>(() => scope.ServiceProvider.GetRequiredService<ProductService>()
            .GetPagedAsync(new ProductQuery(), TestContext.Current.CancellationToken));
    }
}
