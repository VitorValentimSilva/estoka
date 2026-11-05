using Estoka.Application.Common;
using Estoka.Domain.Catalog;
using FluentValidation;

namespace Estoka.Application.Catalog;

/// <summary>
/// Produto para listagem. <see cref="CostPrice"/> e <see cref="MarginPercent"/> vêm nulos
/// quando o usuário não tem a permissão <c>ViewCost</c> (ex.: Vendedor).
/// </summary>
public sealed record ProductListItem(
    Guid Id, string Sku, string Name, string CategoryName, string? SupplierName,
    decimal SalePrice, decimal? CostPrice, decimal? MarginPercent,
    UnitOfMeasure Unit, decimal CurrentStock, decimal MinimumStock, bool IsActive)
{
    public bool IsBelowMinimum => CurrentStock < MinimumStock;
}

public sealed record ProductDetails(
    Guid Id, string Sku, string Name, string? Description, Guid CategoryId, string CategoryName,
    Guid? SupplierId, string? SupplierName, decimal SalePrice, decimal? CostPrice, decimal? MarginPercent,
    UnitOfMeasure Unit, decimal CurrentStock, decimal MinimumStock, bool IsActive, string? ImageUrl,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, string? UpdatedBy);

public sealed record ProductQuery : PageRequest
{
    public string? Search { get; init; }
    public Guid? CategoryId { get; init; }
    public bool BelowMinimumOnly { get; init; }
    public bool? Active { get; init; }
}

/// <summary>Resultado enxuto para autocomplete na tela de pedido.</summary>
public sealed record ProductPick(Guid Id, string Sku, string Name, decimal SalePrice, decimal CurrentStock, UnitOfMeasure Unit, bool IsActive = true)
{
    public override string ToString() => $"{Sku} — {Name}";
}

public sealed class ProductInput
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    public Guid? SupplierId { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public UnitOfMeasure Unit { get; set; } = UnitOfMeasure.Unidade;
    public decimal MinimumStock { get; set; }
    public bool IsActive { get; set; } = true;
    public string? ImageUrl { get; set; }

    /// <summary>Estoque inicial (somente na criação) — gera uma movimentação de ENTRADA.</summary>
    public decimal InitialStock { get; set; }

    public static ProductInput From(ProductDetails p) => new()
    {
        Sku = p.Sku, Name = p.Name, Description = p.Description, CategoryId = p.CategoryId, SupplierId = p.SupplierId,
        CostPrice = p.CostPrice ?? 0, SalePrice = p.SalePrice, Unit = p.Unit, MinimumStock = p.MinimumStock,
        IsActive = p.IsActive, ImageUrl = p.ImageUrl,
    };
}

public sealed class ProductInputValidator : AbstractValidator<ProductInput>
{
    public ProductInputValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().WithMessage("Informe o SKU.").MaximumLength(Product.SkuMaxLength)
            .Matches("^[A-Za-z0-9._-]+$").WithMessage("SKU aceita apenas letras, números, ponto, hífen e sublinhado.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Informe o nome.").MaximumLength(Product.NameMaxLength);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("Selecione a categoria.");
        RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0).WithMessage("Preço de custo não pode ser negativo.");
        RuleFor(x => x.SalePrice).GreaterThan(0).WithMessage("Preço de venda deve ser maior que zero.");
        RuleFor(x => x.SalePrice).GreaterThanOrEqualTo(x => x.CostPrice)
            .WithMessage("Preço de venda está abaixo do custo.");
        RuleFor(x => x.Unit).IsInEnum();
        RuleFor(x => x.MinimumStock).GreaterThanOrEqualTo(0).WithMessage("Estoque mínimo não pode ser negativo.");
        RuleFor(x => x.InitialStock).GreaterThanOrEqualTo(0).WithMessage("Estoque inicial não pode ser negativo.");
        RuleFor(x => x.ImageUrl).Must(u => string.IsNullOrWhiteSpace(u) || Uri.TryCreate(u, UriKind.Absolute, out _))
            .WithMessage("URL da imagem inválida.").MaximumLength(500);
    }
}
