using Estoka.Domain.Common;
using Estoka.Domain.Inventory;

namespace Estoka.UnitTests.Domain;

public class ProductStockTests
{
    [Fact]
    public void Entrada_aumenta_saldo_e_registra_movimentacao_com_sinal()
    {
        var p = TestData.Product();
        var m = p.RegisterMovement(StockMovementType.Entrada, 10, "Compra", "ana", TestData.Now, 9.5m);

        p.CurrentStock.ShouldBe(10);
        m.Quantity.ShouldBe(10);
        m.BalanceAfter.ShouldBe(10);
        p.CostPrice.ShouldBe(9.5m, "a entrada atualiza o custo de reposição");
    }

    [Fact]
    public void Saida_diminui_saldo_com_quantidade_negativa_na_movimentacao()
    {
        var p = TestData.Product(stock: 10);
        var m = p.RegisterMovement(StockMovementType.Saida, 4, "Venda", "ana", TestData.Now);

        p.CurrentStock.ShouldBe(6);
        m.Quantity.ShouldBe(-4);
    }

    [Fact]
    public void Estoque_nunca_fica_negativo()
    {
        var p = TestData.Product(stock: 3);
        var ex = Should.Throw<InsufficientStockException>(() =>
            p.RegisterMovement(StockMovementType.Saida, 4, "Venda", "ana", TestData.Now));

        ex.Available.ShouldBe(3);
        ex.Message.ShouldContain("Estoque insuficiente");
        p.CurrentStock.ShouldBe(3);
    }

    [Fact]
    public void Ajuste_negativo_alem_do_saldo_e_rejeitado()
    {
        var p = TestData.Product(stock: 2);
        Should.Throw<InsufficientStockException>(() =>
            p.RegisterMovement(StockMovementType.Ajuste, -5, "Inventário", "ana", TestData.Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Quantidade_invalida_para_entrada_e_rejeitada(decimal qty)
    {
        var p = TestData.Product();
        Should.Throw<DomainException>(() => p.RegisterMovement(StockMovementType.Entrada, qty, "x", "ana", TestData.Now));
    }

    [Fact]
    public void Saldo_e_igual_a_soma_das_movimentacoes()
    {
        var p = TestData.Product();
        var rnd = new Random(7);
        var movements = new List<StockMovement>();
        for (var i = 0; i < 300; i++)
        {
            try
            {
                var type = (StockMovementType)rnd.Next(1, 5);
                var qty = (decimal)rnd.Next(1, 20) * (type == StockMovementType.Ajuste && rnd.Next(2) == 0 ? -1 : 1);
                movements.Add(p.RegisterMovement(type, qty, "aleatório", "teste", TestData.Now));
            }
            catch (InsufficientStockException)
            {
                // operações rejeitadas não geram movimentação
            }
        }

        p.CurrentStock.ShouldBe(movements.Sum(m => m.Quantity));
        p.CurrentStock.ShouldBeGreaterThanOrEqualTo(0);
        movements.Last().BalanceAfter.ShouldBe(p.CurrentStock);
    }

    [Fact]
    public void Margem_calculada_sobre_preco_de_venda()
    {
        TestData.Product(cost: 6, price: 10).MarginPercent.ShouldBe(40);
    }
}
