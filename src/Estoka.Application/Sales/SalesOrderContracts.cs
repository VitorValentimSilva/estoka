using Estoka.Application.Common;
using Estoka.Domain.Catalog;
using Estoka.Domain.Customers;
using Estoka.Domain.Sales;
using FluentValidation;

namespace Estoka.Application.Sales;

public sealed record SalesOrderListItem(
    Guid Id, long Number, DateTimeOffset OrderDate, Guid CustomerId, string CustomerName, SalesOrderStatus Status,
    PaymentMethod PaymentMethod, int ItemCount, decimal Total, string SellerName)
{
    public string Code => $"PED-{Number:D6}";
}

public sealed record SalesOrderItemDto(
    Guid Id, Guid ProductId, string Sku, string ProductName, UnitOfMeasure Unit, decimal Quantity, decimal UnitPrice,
    decimal Discount, decimal Total, decimal AvailableStock);

public sealed record SalesOrderCustomer(Guid Id, string Name, string Document, PersonType Type, string? Email, string? Phone, Address Address);

public sealed record SalesOrderDetails(
    Guid Id, long Number, DateTimeOffset OrderDate, SalesOrderStatus Status, PaymentMethod PaymentMethod,
    SalesOrderCustomer Customer, string SellerName, string? Notes, decimal Subtotal, decimal Discount, decimal Total,
    IReadOnlyList<SalesOrderItemDto> Items,
    DateTimeOffset? ConfirmedAt, DateTimeOffset? InvoicedAt, DateTimeOffset? DeliveredAt, DateTimeOffset? CanceledAt,
    string? CancelReason)
{
    public string Code => $"PED-{Number:D6}";
    public bool IsEditable => Status == SalesOrderStatus.Rascunho;
    public IReadOnlyList<SalesOrderStatus> AllowedTransitions => SalesOrder.AllowedTransitions(Status);
}

public sealed record SalesOrderQuery : PageRequest
{
    public SalesOrderStatus? Status { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public Guid? CustomerId { get; init; }
    public string? Search { get; init; }
}

public sealed class SalesOrderInput
{
    public Guid CustomerId { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Pix;
    public decimal Discount { get; set; }
    public string? Notes { get; set; }
    public List<SalesOrderItemInput> Items { get; set; } = [];
}

public sealed class SalesOrderItemInput
{
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; } = 1;
    /// <summary>Preço unitário. Se nulo (ou se o usuário não pode alterar preço), usa o preço de venda atual.</summary>
    public decimal? UnitPrice { get; set; }
    public decimal Discount { get; set; }
}

public sealed class SalesOrderInputValidator : AbstractValidator<SalesOrderInput>
{
    public SalesOrderInputValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty().WithMessage("Selecione o cliente.");
        RuleFor(x => x.PaymentMethod).IsInEnum().WithMessage("Forma de pagamento inválida.");
        RuleFor(x => x.Discount).GreaterThanOrEqualTo(0).WithMessage("Desconto não pode ser negativo.");
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Items).NotEmpty().WithMessage("Adicione ao menos um item.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty().WithMessage("Produto é obrigatório.");
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Quantidade deve ser maior que zero.");
            item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0).When(i => i.UnitPrice.HasValue)
                .WithMessage("Preço não pode ser negativo.");
            item.RuleFor(i => i.Discount).GreaterThanOrEqualTo(0).WithMessage("Desconto não pode ser negativo.");
        });
    }
}

public static class SalesOrderLabels
{
    public static string Label(this SalesOrderStatus status) => status switch
    {
        SalesOrderStatus.Rascunho => "Rascunho",
        SalesOrderStatus.Confirmado => "Confirmado",
        SalesOrderStatus.Faturado => "Faturado",
        SalesOrderStatus.Entregue => "Entregue",
        SalesOrderStatus.Cancelado => "Cancelado",
        _ => status.ToString(),
    };

    public static string Label(this PaymentMethod method) => method switch
    {
        PaymentMethod.Dinheiro => "Dinheiro",
        PaymentMethod.Pix => "Pix",
        PaymentMethod.CartaoCredito => "Cartão de crédito",
        PaymentMethod.CartaoDebito => "Cartão de débito",
        PaymentMethod.Boleto => "Boleto",
        PaymentMethod.APrazo => "A prazo",
        _ => method.ToString(),
    };
}
