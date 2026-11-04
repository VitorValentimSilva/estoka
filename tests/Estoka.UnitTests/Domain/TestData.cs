using Estoka.Domain.Catalog;
using Estoka.Domain.Inventory;

namespace Estoka.UnitTests.Domain;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 11, 4, 15, 0, 0, TimeSpan.Zero);

    public static Product Product(decimal stock = 0, decimal cost = 10m, decimal price = 15m, decimal minimum = 5)
    {
        var p = new Product("TST-001", "Produto de teste", null, Guid.NewGuid(), null, cost, price, UnitOfMeasure.Unidade, minimum);
        if (stock > 0) p.RegisterMovement(StockMovementType.Entrada, stock, "Estoque inicial", "teste", Now);
        return p;
    }
}
