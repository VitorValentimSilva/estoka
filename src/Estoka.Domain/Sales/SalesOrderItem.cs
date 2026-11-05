using Estoka.Domain.Catalog;
using Estoka.Domain.Common;

namespace Estoka.Domain.Sales;

public sealed class SalesOrderItem : Entity
{
    private SalesOrderItem() { ProductName = string.Empty; Sku = string.Empty; }

    internal SalesOrderItem(Guid salesOrderId, Product product, decimal quantity, decimal unitPrice, decimal discount)
    {
        SalesOrderId = salesOrderId;
        ProductId = product.Id;
        ProductName = product.Name;
        Sku = product.Sku;
        UnitCost = product.CostPrice;
        Change(quantity, unitPrice, discount);
    }

    public Guid SalesOrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public Product? Product { get; private set; }

    /// <summary>Nome e SKU no momento da venda (snapshot).</summary>
    public string ProductName { get; private set; }
    public string Sku { get; private set; }

    public decimal Quantity { get; private set; }
    /// <summary>Preço de venda unitário no momento do pedido.</summary>
    public decimal UnitPrice { get; private set; }
    /// <summary>Custo unitário no momento do pedido (para margem e curva ABC).</summary>
    public decimal UnitCost { get; private set; }
    /// <summary>Desconto da linha, em R$.</summary>
    public decimal Discount { get; private set; }
    public decimal Total { get; private set; }

    internal void Change(decimal quantity, decimal unitPrice, decimal discount)
    {
        Quantity = Guard.Positive(quantity, "Quantidade");
        UnitPrice = Guard.NotNegative(unitPrice, "Preço unitário");
        Discount = Guard.NotNegative(discount, "Desconto do item");
        var gross = Math.Round(Quantity * UnitPrice, 2);
        if (Discount > gross) throw new DomainException($"O desconto do item \"{ProductName}\" não pode ser maior que o valor do item.");
        Total = gross - Discount;
    }
}
