using System.Net;
using System.Net.Http.Json;
using Estoka.Application.Catalog;
using Estoka.Application.Security;
using Estoka.Domain.Common;
using Estoka.Infrastructure.Persistence;
using Estoka.Infrastructure.Seed;
using Estoka.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Estoka.IntegrationTests;

[Collection(AppCollection.Name)]
public class BarcodeTests(EstokaFactory factory)
{
    private static int _seq;

    /// <summary>EAN-13 único por teste, fora da faixa da demo (prefixo 29).</summary>
    private static string NewBarcode()
    {
        var body = $"29{Interlocked.Increment(ref _seq):0000}{Random.Shared.Next(0, 999999):000000}";
        return body + Gtin.CheckDigit(body);
    }

    private async Task<(Guid Id, string Barcode)> CreateWithBarcodeAsync(bool active = true)
    {
        var barcode = NewBarcode();
        await using var scope = factory.ScopeAs(Roles.Gerente);
        var categories = await scope.ServiceProvider.GetRequiredService<CategoryService>().LookupAsync();
        var id = await scope.ServiceProvider.GetRequiredService<ProductService>().CreateAsync(new ProductInput
        {
            Sku = $"BC-{Guid.NewGuid():N}"[..15], Name = "Produto com código", CategoryId = categories[0].Id,
            CostPrice = 5, SalePrice = 8, Barcode = barcode, IsActive = active, InitialStock = 4,
        });
        return (id, barcode);
    }

    [Fact]
    public async Task Seed_preenche_codigos_validos_e_unicos_em_todos_os_produtos()
    {
        await using var scope = factory.ScopeAs(Roles.Admin);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var demo = await db.Products.Where(p => p.Sku.Length == 7).Select(p => p.Barcode).ToListAsync(TestContext.Current.CancellationToken);
        demo.Count.ShouldBe(120);
        demo.ShouldAllBe(b => b != null && Gtin.IsValid(b));
        demo.Distinct().Count().ShouldBe(120);
    }

    [Fact]
    public async Task Busca_pelo_codigo_no_pedido_e_na_lista()
    {
        var ct = TestContext.Current.CancellationToken;
        var (id, barcode) = await CreateWithBarcodeAsync();
        await using var scope = factory.ScopeAs(Roles.Vendedor);
        var products = scope.ServiceProvider.GetRequiredService<ProductService>();

        (await products.FindByBarcodeAsync(barcode, ct: ct))!.Id.ShouldBe(id);
        (await products.SearchForSaleAsync(barcode, 5, ct)).ShouldContain(p => p.Id == id);
        (await products.GetPagedAsync(new ProductQuery { Search = barcode }, ct)).Items.ShouldHaveSingleItem().Barcode.ShouldBe(barcode);
        (await products.FindByBarcodeAsync(NewBarcode(), ct: ct)).ShouldBeNull();
        await Should.ThrowAsync<DomainException>(() => products.FindByBarcodeAsync("7891000315508", ct: ct));
    }

    [Fact]
    public async Task Produto_inativo_so_e_achado_pelo_estoque()
    {
        var ct = TestContext.Current.CancellationToken;
        var (id, barcode) = await CreateWithBarcodeAsync(active: false);
        await using var scope = factory.ScopeAs(Roles.Gerente);
        var products = scope.ServiceProvider.GetRequiredService<ProductService>();
        (await products.FindByBarcodeAsync(barcode, ct: ct)).ShouldBeNull();
        (await products.FindByBarcodeAsync(barcode, includeInactive: true, ct))!.Id.ShouldBe(id);
    }

    [Fact]
    public async Task Codigo_duplicado_e_invalido_sao_rejeitados()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, barcode) = await CreateWithBarcodeAsync();
        await using var scope = factory.ScopeAs(Roles.Gerente);
        var categories = await scope.ServiceProvider.GetRequiredService<CategoryService>().LookupAsync(ct);
        var products = scope.ServiceProvider.GetRequiredService<ProductService>();
        var input = new ProductInput { Sku = $"BC-{Guid.NewGuid():N}"[..15], Name = "Outro", CategoryId = categories[0].Id, SalePrice = 1, Barcode = barcode };

        var dup = await Should.ThrowAsync<DomainException>(() => products.CreateAsync(input, ct));
        dup.Message.ShouldContain("já pertence");

        input.Barcode = "7891000315508";
        await Should.ThrowAsync<FluentValidation.ValidationException>(() => products.CreateAsync(input, ct));
    }

    [Fact]
    public async Task Api_busca_por_codigo_de_barras()
    {
        var ct = TestContext.Current.CancellationToken;
        var (id, barcode) = await CreateWithBarcodeAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", EstokaFactory.ApiKey);

        var found = await client.GetFromJsonAsync<ProductPick>($"/api/v1/products/barcode/{barcode}",
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }, ct);
        found!.Id.ShouldBe(id);
        (await client.GetAsync($"/api/v1/products/barcode/{NewBarcode()}", ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/api/v1/products/barcode/7891000315508", ct)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Backfill_preenche_codigo_dos_produtos_de_demo_sem_codigo()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = factory.ScopeAs(Roles.Admin);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Products.Where(p => p.Sku == "MER-002").ExecuteUpdateAsync(s => s.SetProperty(p => p.Barcode, (string?)null), ct);

        var filled = await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().BackfillDemoBarcodesAsync(ct);

        filled.ShouldBe(1);
        (await db.Products.AsNoTracking().SingleAsync(p => p.Sku == "MER-002", ct)).Barcode.ShouldBe(DemoDataSeeder.DemoBarcode(2, 2));
        (await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().BackfillDemoBarcodesAsync(ct)).ShouldBe(0);
    }
}
