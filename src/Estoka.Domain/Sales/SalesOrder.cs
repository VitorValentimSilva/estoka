using Estoka.Domain.Catalog;
using Estoka.Domain.Common;
using Estoka.Domain.Customers;

namespace Estoka.Domain.Sales;

public enum SalesOrderStatus
{
    Rascunho = 1,
    Confirmado = 2,
    Faturado = 3,
    Entregue = 4,
    Cancelado = 5,
}

public enum PaymentMethod
{
    Dinheiro = 1,
    Pix = 2,
    CartaoCredito = 3,
    CartaoDebito = 4,
    Boleto = 5,
    APrazo = 6,
}

public sealed class SalesOrder : AuditableEntity
{
    private readonly List<SalesOrderItem> _items = [];

    private SalesOrder() { SellerName = string.Empty; }

    public SalesOrder(Guid customerId, DateTimeOffset orderDate, PaymentMethod paymentMethod, string sellerName, string? notes = null)
    {
        if (customerId == Guid.Empty) throw new DomainException("Cliente é obrigatório.");
        CustomerId = customerId;
        OrderDate = orderDate;
        PaymentMethod = paymentMethod;
        SellerName = Guard.NotEmpty(sellerName, "Vendedor", 150);
        Notes = Guard.Optional(notes, "Observações", 1000);
        Status = SalesOrderStatus.Rascunho;
    }

    /// <summary>Número sequencial legível, gerado por sequence no banco.</summary>
    public long Number { get; private set; }
    public string Code => Number > 0 ? $"PED-{Number:D6}" : "PED-NOVO";
    public Guid CustomerId { get; private set; }
    public Customer? Customer { get; private set; }
    public DateTimeOffset OrderDate { get; private set; }
    public SalesOrderStatus Status { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public string SellerName { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>Desconto aplicado no total do pedido (valor em R$).</summary>
    public decimal Discount { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal Total { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }
    public DateTimeOffset? InvoicedAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public DateTimeOffset? CanceledAt { get; private set; }
    public string? CancelReason { get; private set; }

    /// <summary>Token de concorrência otimista (xmin).</summary>
    public uint Version { get; private set; }

    public IReadOnlyCollection<SalesOrderItem> Items => _items.AsReadOnly();

    public bool IsEditable => Status == SalesOrderStatus.Rascunho;

    /// <summary>Indica se o estoque dos itens já foi baixado (pedido confirmado ou faturado).</summary>
    public bool HasStockCommitted => Status is SalesOrderStatus.Confirmado or SalesOrderStatus.Faturado or SalesOrderStatus.Entregue;

    public void UpdateHeader(Guid customerId, PaymentMethod paymentMethod, decimal discount, string? notes)
    {
        EnsureEditable();
        if (customerId == Guid.Empty) throw new DomainException("Cliente é obrigatório.");
        CustomerId = customerId;
        PaymentMethod = paymentMethod;
        Notes = Guard.Optional(notes, "Observações", 1000);
        SetDiscount(discount);
    }

    public void SetDiscount(decimal discount)
    {
        EnsureEditable();
        Guard.NotNegative(discount, "Desconto");
        if (discount > Subtotal)
            throw new DomainException("O desconto não pode ser maior que o subtotal do pedido.");
        Discount = discount;
        Recalculate();
    }

    /// <summary>Remove todos os itens (usado ao salvar o rascunho com a lista completa de itens).</summary>
    public void ClearItems()
    {
        EnsureEditable();
        _items.Clear();
        Discount = 0;
        Recalculate();
    }

    public SalesOrderItem AddItem(Product product, decimal quantity, decimal? unitPrice = null, decimal discount = 0)
    {
        EnsureEditable();
        if (!product.IsActive) throw new DomainException($"O produto \"{product.Name}\" está inativo.");
        var existing = _items.FirstOrDefault(i => i.ProductId == product.Id);
        if (existing is not null)
        {
            existing.Change(existing.Quantity + Guard.Positive(quantity, "Quantidade"), existing.UnitPrice, existing.Discount + discount);
            Recalculate();
            return existing;
        }

        var item = new SalesOrderItem(Id, product, quantity, unitPrice ?? product.SalePrice, discount);
        _items.Add(item);
        Recalculate();
        return item;
    }

    public void ChangeItem(Guid itemId, decimal quantity, decimal unitPrice, decimal discount)
    {
        EnsureEditable();
        var item = _items.FirstOrDefault(i => i.Id == itemId) ?? throw new DomainException("Item não encontrado no pedido.");
        item.Change(quantity, unitPrice, discount);
        Recalculate();
    }

    public void RemoveItem(Guid itemId)
    {
        EnsureEditable();
        var item = _items.FirstOrDefault(i => i.Id == itemId) ?? throw new DomainException("Item não encontrado no pedido.");
        _items.Remove(item);
        Recalculate();
    }

    public void Confirm(DateTimeOffset at)
    {
        EnsureTransition(SalesOrderStatus.Rascunho, SalesOrderStatus.Confirmado);
        if (_items.Count == 0) throw new DomainException("Adicione ao menos um item antes de confirmar o pedido.");
        Recalculate();
        if (Total < 0) throw new DomainException("O desconto não pode ser maior que o subtotal do pedido.");
        Status = SalesOrderStatus.Confirmado;
        ConfirmedAt = at;
    }

    public void Invoice(DateTimeOffset at)
    {
        EnsureTransition(SalesOrderStatus.Confirmado, SalesOrderStatus.Faturado);
        Status = SalesOrderStatus.Faturado;
        InvoicedAt = at;
    }

    public void Deliver(DateTimeOffset at)
    {
        EnsureTransition(SalesOrderStatus.Faturado, SalesOrderStatus.Entregue);
        Status = SalesOrderStatus.Entregue;
        DeliveredAt = at;
    }

    /// <summary>Cancela o pedido. Retorna true se havia estoque baixado que precisa ser devolvido.</summary>
    public bool Cancel(DateTimeOffset at, string reason)
    {
        if (Status is SalesOrderStatus.Entregue)
            throw new InvalidStatusTransitionException("Pedidos entregues não podem ser cancelados. Registre uma devolução.");
        if (Status is SalesOrderStatus.Cancelado)
            throw new InvalidStatusTransitionException("O pedido já está cancelado.");
        var hadStock = HasStockCommitted;
        Status = SalesOrderStatus.Cancelado;
        CanceledAt = at;
        CancelReason = Guard.NotEmpty(reason, "Motivo do cancelamento", 300);
        return hadStock;
    }

    public static IReadOnlyList<SalesOrderStatus> AllowedTransitions(SalesOrderStatus from) => from switch
    {
        SalesOrderStatus.Rascunho => [SalesOrderStatus.Confirmado, SalesOrderStatus.Cancelado],
        SalesOrderStatus.Confirmado => [SalesOrderStatus.Faturado, SalesOrderStatus.Cancelado],
        SalesOrderStatus.Faturado => [SalesOrderStatus.Entregue, SalesOrderStatus.Cancelado],
        _ => [],
    };

    private void EnsureTransition(SalesOrderStatus expected, SalesOrderStatus target)
    {
        if (Status != expected)
            throw new InvalidStatusTransitionException(
                $"Não é possível mudar o pedido de \"{Status}\" para \"{target}\".");
    }

    private void EnsureEditable()
    {
        if (!IsEditable)
            throw new DomainException("Somente pedidos em rascunho podem ser alterados.");
    }

    private void Recalculate()
    {
        Subtotal = _items.Sum(i => i.Total);
        Total = Subtotal - Discount;
    }
}
