using Estoka.Domain.Common;
using Estoka.Domain.Inventory;

namespace Estoka.Domain.Catalog;

public sealed class Product : SoftDeletableEntity
{
    public const int SkuMaxLength = 30;
    public const int NameMaxLength = 150;

    private Product() { Sku = string.Empty; Name = string.Empty; }

    public Product(string sku, string name, string? description, Guid categoryId, Guid? supplierId,
        decimal costPrice, decimal salePrice, UnitOfMeasure unit, decimal minimumStock, string? imageUrl = null)
    {
        Sku = string.Empty;
        Name = string.Empty;
        Update(sku, name, description, categoryId, supplierId, costPrice, salePrice, unit, minimumStock, true, imageUrl);
    }

    public string Sku { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public Guid CategoryId { get; private set; }
    public Category? Category { get; private set; }
    public Guid? SupplierId { get; private set; }
    public Supplier? Supplier { get; private set; }
    public decimal CostPrice { get; private set; }
    public decimal SalePrice { get; private set; }
    public UnitOfMeasure Unit { get; private set; }

    /// <summary>Saldo atual. Só muda via <see cref="RegisterMovement"/> — sempre igual à soma das movimentações.</summary>
    public decimal CurrentStock { get; private set; }
    public decimal MinimumStock { get; private set; }
    public bool IsActive { get; private set; }
    public string? ImageUrl { get; private set; }

    /// <summary>Token de concorrência otimista (coluna de sistema <c>xmin</c> do PostgreSQL).</summary>
    public uint Version { get; private set; }

    public bool IsBelowMinimum => CurrentStock < MinimumStock;

    /// <summary>Margem bruta sobre o preço de venda, em percentual.</summary>
    public decimal MarginPercent => SalePrice == 0 ? 0 : Math.Round((SalePrice - CostPrice) / SalePrice * 100, 2);

    public void Update(string sku, string name, string? description, Guid categoryId, Guid? supplierId,
        decimal costPrice, decimal salePrice, UnitOfMeasure unit, decimal minimumStock, bool isActive, string? imageUrl)
    {
        if (categoryId == Guid.Empty) throw new DomainException("Categoria é obrigatória.");
        if (!Enum.IsDefined(unit)) throw new DomainException("Unidade inválida.");
        Sku = Guard.NotEmpty(sku, "SKU", SkuMaxLength).ToUpperInvariant();
        Name = Guard.NotEmpty(name, "Nome", NameMaxLength);
        Description = Guard.Optional(description, "Descrição", 1000);
        CategoryId = categoryId;
        SupplierId = supplierId == Guid.Empty ? null : supplierId;
        CostPrice = Guard.NotNegative(costPrice, "Preço de custo");
        SalePrice = Guard.NotNegative(salePrice, "Preço de venda");
        Unit = unit;
        MinimumStock = Guard.NotNegative(minimumStock, "Estoque mínimo");
        IsActive = isActive;
        ImageUrl = Guard.Optional(imageUrl, "URL da imagem", 500);
    }

    public void Deactivate() => IsActive = false;

    /// <summary>
    /// Registra uma movimentação de estoque e atualiza o saldo. A quantidade é informada sempre positiva,
    /// exceto em <see cref="StockMovementType.Ajuste"/>, em que o sinal indica acréscimo ou redução.
    /// Lança <see cref="InsufficientStockException"/> se o saldo ficaria negativo.
    /// </summary>
    public StockMovement RegisterMovement(StockMovementType type, decimal quantity, string reason, string? userName,
        DateTimeOffset occurredAt, decimal? unitCost = null, Guid? salesOrderId = null)
    {
        if (quantity == 0) throw new DomainException("Quantidade deve ser diferente de zero.");
        if (type != StockMovementType.Ajuste && quantity < 0)
            throw new DomainException("Quantidade deve ser maior que zero.");

        var delta = type switch
        {
            StockMovementType.Entrada or StockMovementType.Devolucao => quantity,
            StockMovementType.Saida => -quantity,
            StockMovementType.Ajuste => quantity,
            _ => throw new DomainException("Tipo de movimentação inválido."),
        };

        var newBalance = CurrentStock + delta;
        if (newBalance < 0)
            throw new InsufficientStockException(Name, CurrentStock, Math.Abs(delta));

        if (type == StockMovementType.Entrada && unitCost is > 0)
            CostPrice = unitCost.Value;

        CurrentStock = newBalance;
        return new StockMovement(Id, type, delta, newBalance, unitCost ?? CostPrice, reason, userName, occurredAt, salesOrderId);
    }
}
