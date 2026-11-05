using System.Diagnostics.Metrics;
using Estoka.Application.Common;
using Estoka.Application.Sales;
using Estoka.Application.Security;
using Estoka.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Estoka.IntegrationTests;

[Collection(AppCollection.Name)]
public class TelemetryTests(EstokaFactory factory)
{
    [Fact]
    public async Task Confirmar_pedido_emite_metricas_de_negocio()
    {
        var ct = TestContext.Current.CancellationToken;
        var productId = await factory.CreateProductAsync(stock: 10, price: 25m);
        var customerId = await factory.CreateCustomerAsync();

        long confirmed = 0;
        double revenue = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == EstokaTelemetry.Name) l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((i, v, _, _) => { if (i.Name == "estoka.orders.confirmed") Interlocked.Add(ref confirmed, v); });
        listener.SetMeasurementEventCallback<double>((i, v, _, _) => { if (i.Name == "estoka.orders.revenue") revenue += v; });
        listener.Start();

        await using var scope = factory.ScopeAs(Roles.Vendedor);
        var orders = scope.ServiceProvider.GetRequiredService<SalesOrderService>();
        var id = await orders.CreateDraftAsync(new SalesOrderInput
        {
            CustomerId = customerId, Items = [new SalesOrderItemInput { ProductId = productId, Quantity = 2 }],
        }, ct);
        await orders.ConfirmAsync(id, ct);

        confirmed.ShouldBeGreaterThanOrEqualTo(1);
        revenue.ShouldBeGreaterThanOrEqualTo(50);
    }
}
