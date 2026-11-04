using System.Net;
using Estoka.Application.Common;
using Estoka.Application.Dashboard;
using Estoka.Application.Security;
using Estoka.Infrastructure.Persistence;
using Estoka.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Estoka.IntegrationTests;

[Collection(AppCollection.Name)]
public class DashboardAndDocumentsTests(EstokaFactory factory)
{
    [Fact]
    public async Task Dashboard_bate_com_os_pedidos_vendidos_do_mes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = factory.ScopeAs(Roles.Vendedor);
        var data = await scope.ServiceProvider.GetRequiredService<DashboardService>().GetAsync(ct);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var today = BrazilTime.Today(TimeProvider.System);
        var monthStart = BrazilTime.StartOfDayUtc(new DateOnly(today.Year, today.Month, 1));
        var expected = await db.SalesOrders
            .Where(o => DashboardService.SoldStatuses.Contains(o.Status) && o.OrderDate >= monthStart)
            .SumAsync(o => o.Total, ct);

        data.Kpis.MonthRevenue.ShouldBe(expected);
        data.Last30Days.Count.ShouldBe(30);
        data.Last30Days[^1].Date.ShouldBe(today);
        data.TopProducts.Count.ShouldBeLessThanOrEqualTo(5);
        data.TopProducts.ShouldBe(data.TopProducts.OrderByDescending(p => p.Revenue).ToList());
        data.LowStock.ShouldAllBe(i => i.CurrentStock < i.MinimumStock);
        data.Kpis.LowStockCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Pdf_do_pedido_e_gerado_para_usuario_logado()
    {
        var ct = TestContext.Current.CancellationToken;
        Guid orderId;
        await using (var scope = factory.ScopeAs(Roles.Admin))
            orderId = await scope.ServiceProvider.GetRequiredService<AppDbContext>().SalesOrders.OrderBy(o => o.Number).Select(o => o.Id).FirstAsync(ct);

        using var client = factory.CreateBrowserClient();
        (await client.GetAsync($"/pedidos/{orderId}/pdf", ct)).IsRedirectTo("/login").ShouldBeTrue();

        await client.LoginAsync("vendedor@estoka.app");
        var response = await client.GetAsync($"/pedidos/{orderId}/pdf", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
    }

    [Fact]
    public async Task Pdf_de_pedido_inexistente_retorna_problem_details_404()
    {
        using var client = factory.CreateBrowserClient();
        await client.LoginAsync("gerente@estoka.app");
        var response = await client.GetAsync($"/pedidos/{Guid.NewGuid()}/pdf", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }
}
