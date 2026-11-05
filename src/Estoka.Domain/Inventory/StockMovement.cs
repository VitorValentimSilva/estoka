using Estoka.Domain.Common;

namespace Estoka.Domain.Inventory;

public enum StockMovementType
{
    Entrada = 1,
    Saida = 2,
    Ajuste = 3,
    Devolucao = 4,
}

/// <summary>
/// Lançamento imutável no "livro-razão" de estoque. <see cref="Quantity"/> é armazenada COM sinal
/// (efeito no saldo), de forma que a soma das movimentações de um produto é igual ao seu saldo atual.
/// </summary>
public sealed class StockMovement : Entity
{
    private StockMovement() { Reason = string.Empty; }

    internal StockMovement(Guid productId, StockMovementType type, decimal quantity, decimal balanceAfter,
        decimal unitCost, string reason, string? userName, DateTimeOffset occurredAt, Guid? salesOrderId)
    {
        ProductId = productId;
        Type = type;
        Quantity = quantity;
        BalanceAfter = balanceAfter;
        UnitCost = unitCost;
        Reason = Guard.NotEmpty(reason, "Motivo", 200);
        UserName = userName;
        OccurredAt = occurredAt;
        SalesOrderId = salesOrderId;
    }

    public Guid ProductId { get; private set; }
    public Catalog.Product? Product { get; private set; }
    public StockMovementType Type { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal BalanceAfter { get; private set; }
    public decimal UnitCost { get; private set; }
    public string Reason { get; private set; }
    public Guid? SalesOrderId { get; private set; }
    public Sales.SalesOrder? SalesOrder { get; private set; }
    public string? UserName { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
}
