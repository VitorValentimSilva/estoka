using Estoka.Domain.Sales;

namespace Estoka.Web.Api;

/// <summary>Página de resultados (página base 1).</summary>
public sealed record ApiPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);
}

public sealed record CreateOrderRequest(
    Guid CustomerId,
    PaymentMethod PaymentMethod,
    IReadOnlyList<CreateOrderItemRequest> Items,
    decimal Discount = 0,
    string? Notes = null,
    bool Confirm = false);

public sealed record CreateOrderItemRequest(Guid ProductId, decimal Quantity, decimal Discount = 0);

public sealed record CancelOrderRequest(string Reason);
