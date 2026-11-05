using Estoka.Application.Common;
using Estoka.Domain.Catalog;
using Estoka.Domain.Inventory;
using FluentValidation;

namespace Estoka.Application.Inventory;

public sealed record StockMovementDto(
    Guid Id, DateTimeOffset OccurredAt, Guid ProductId, string Sku, string ProductName, StockMovementType Type,
    decimal Quantity, decimal BalanceAfter, decimal? UnitCost, string Reason, Guid? SalesOrderId, long? SalesOrderNumber,
    string? UserName, UnitOfMeasure Unit);

public sealed record StockMovementQuery : PageRequest
{
    public Guid? ProductId { get; init; }
    public StockMovementType? Type { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public string? Search { get; init; }
}

public sealed record LowStockItem(Guid ProductId, string Sku, string Name, string CategoryName, decimal CurrentStock,
    decimal MinimumStock, UnitOfMeasure Unit)
{
    public decimal Missing => MinimumStock - CurrentStock;
}

public sealed class StockEntryInput
{
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public string Reason { get; set; } = "Compra de fornecedor";
}

public sealed class StockAdjustmentInput
{
    public Guid ProductId { get; set; }
    /// <summary>Quantidade efetivamente contada no inventário. O ajuste é a diferença para o saldo atual.</summary>
    public decimal CountedQuantity { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class StockEntryInputValidator : AbstractValidator<StockEntryInput>
{
    public StockEntryInputValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Selecione o produto.");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantidade deve ser maior que zero.");
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0).When(x => x.UnitCost.HasValue).WithMessage("Custo não pode ser negativo.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Informe o motivo.").MaximumLength(200);
    }
}

public sealed class StockAdjustmentInputValidator : AbstractValidator<StockAdjustmentInput>
{
    public StockAdjustmentInputValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Selecione o produto.");
        RuleFor(x => x.CountedQuantity).GreaterThanOrEqualTo(0).WithMessage("A quantidade contada não pode ser negativa.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Informe o motivo do ajuste.").MaximumLength(200);
    }
}
