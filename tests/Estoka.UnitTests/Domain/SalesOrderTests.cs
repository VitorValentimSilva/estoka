using Estoka.Domain.Common;
using Estoka.Domain.Sales;

namespace Estoka.UnitTests.Domain;

public class SalesOrderTests
{
    private static SalesOrder NewOrder() => new(Guid.NewGuid(), TestData.Now, PaymentMethod.Pix, "Juliana");

    [Fact]
    public void Totais_consideram_desconto_por_item_e_do_pedido()
    {
        var order = NewOrder();
        order.AddItem(TestData.Product(price: 10), 3, discount: 2);   // 28
        order.AddItem(TestData.Product(price: 4.5m), 2);              // 9
        order.SetDiscount(5);

        order.Subtotal.ShouldBe(37);
        order.Total.ShouldBe(32);
    }

    [Fact]
    public void Mesmo_produto_adicionado_duas_vezes_soma_quantidade()
    {
        var order = NewOrder();
        var p = TestData.Product(price: 10);
        order.AddItem(p, 1);
        order.AddItem(p, 2);

        order.Items.Count.ShouldBe(1);
        order.Items.Single().Quantity.ShouldBe(3);
    }

    [Fact]
    public void Desconto_maior_que_subtotal_e_rejeitado()
    {
        var order = NewOrder();
        order.AddItem(TestData.Product(price: 10), 1);
        Should.Throw<DomainException>(() => order.SetDiscount(11));
    }

    [Fact]
    public void Fluxo_completo_de_status()
    {
        var order = NewOrder();
        order.AddItem(TestData.Product(price: 10), 1);

        order.Confirm(TestData.Now);
        order.Status.ShouldBe(SalesOrderStatus.Confirmado);
        order.Invoice(TestData.Now);
        order.Status.ShouldBe(SalesOrderStatus.Faturado);
        order.Deliver(TestData.Now);
        order.Status.ShouldBe(SalesOrderStatus.Entregue);
        order.DeliveredAt.ShouldNotBeNull();
    }

    [Fact]
    public void Nao_pula_etapas_do_fluxo()
    {
        var order = NewOrder();
        order.AddItem(TestData.Product(price: 10), 1);
        Should.Throw<InvalidStatusTransitionException>(() => order.Invoice(TestData.Now));
        Should.Throw<InvalidStatusTransitionException>(() => order.Deliver(TestData.Now));
    }

    [Fact]
    public void Pedido_sem_itens_nao_pode_ser_confirmado()
        => Should.Throw<DomainException>(() => NewOrder().Confirm(TestData.Now));

    [Fact]
    public void Pedido_confirmado_nao_pode_ser_editado()
    {
        var order = NewOrder();
        order.AddItem(TestData.Product(price: 10), 1);
        order.Confirm(TestData.Now);
        Should.Throw<DomainException>(() => order.AddItem(TestData.Product(), 1));
    }

    [Fact]
    public void Cancelar_rascunho_nao_exige_devolucao_de_estoque()
    {
        var order = NewOrder();
        order.AddItem(TestData.Product(price: 10), 1);
        order.Cancel(TestData.Now, "teste").ShouldBeFalse();
        order.Status.ShouldBe(SalesOrderStatus.Cancelado);
    }

    [Fact]
    public void Cancelar_pedido_confirmado_indica_devolucao_de_estoque()
    {
        var order = NewOrder();
        order.AddItem(TestData.Product(price: 10), 1);
        order.Confirm(TestData.Now);
        order.Cancel(TestData.Now, "teste").ShouldBeTrue();
    }

    [Fact]
    public void Pedido_entregue_nao_pode_ser_cancelado()
    {
        var order = NewOrder();
        order.AddItem(TestData.Product(price: 10), 1);
        order.Confirm(TestData.Now);
        order.Invoice(TestData.Now);
        order.Deliver(TestData.Now);
        Should.Throw<InvalidStatusTransitionException>(() => order.Cancel(TestData.Now, "x"));
    }

    [Fact]
    public void Item_guarda_snapshot_de_preco_e_custo()
    {
        var product = TestData.Product(cost: 7, price: 12);
        var order = NewOrder();
        order.AddItem(product, 2);
        product.Update(product.Sku, product.Name, null, product.CategoryId, null, 9, 20, product.Unit, 1, true, null);

        var item = order.Items.Single();
        item.UnitPrice.ShouldBe(12);
        item.UnitCost.ShouldBe(7);
    }

    [Theory]
    [InlineData(SalesOrderStatus.Rascunho, new[] { SalesOrderStatus.Confirmado, SalesOrderStatus.Cancelado })]
    [InlineData(SalesOrderStatus.Entregue, new SalesOrderStatus[0])]
    public void Transicoes_permitidas(SalesOrderStatus from, SalesOrderStatus[] expected)
        => SalesOrder.AllowedTransitions(from).ShouldBe(expected);
}
