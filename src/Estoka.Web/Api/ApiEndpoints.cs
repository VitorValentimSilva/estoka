using Estoka.Application.Catalog;
using Estoka.Application.Common;
using Estoka.Application.Customers;
using Estoka.Application.Inventory;
using Estoka.Application.Sales;
using Estoka.Domain.Inventory;
using Estoka.Domain.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Estoka.Web.Api;

/// <summary>
/// API REST v1. Os endpoints apenas traduzem HTTP ⇄ casos de uso: validação, autorização e regras
/// ficam nos serviços da Application (os mesmos usados pelas telas).
/// </summary>
public static class ApiEndpoints
{
    public static void MapApiV1(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = ApiKeyAuthOptions.Scheme })
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        var products = api.MapGroup("/products").WithTags("Produtos");
        products.MapGet("/", async (ProductService service, string? search, Guid? categoryId, bool? belowMinimum, int? page, int? pageSize,
                CancellationToken ct) =>
            {
                var (p, size) = Paging(page, pageSize);
                var result = await service.GetPagedAsync(new ProductQuery
                {
                    Page = p - 1, PageSize = size, Search = search, CategoryId = categoryId, BelowMinimumOnly = belowMinimum ?? false, Active = true,
                }, ct);
                return TypedResults.Ok(new ApiPage<ProductListItem>(result.Items, p, size, result.TotalCount));
            })
            .WithName("ListProducts").WithSummary("Lista produtos ativos (busca por nome/SKU, filtro por categoria e estoque baixo)");
        products.MapGet("/{id:guid}", async (Guid id, ProductService service, CancellationToken ct) => TypedResults.Ok(await service.GetAsync(id, ct)))
            .WithName("GetProduct").WithSummary("Detalhes de um produto").ProducesProblem(StatusCodes.Status404NotFound);

        var stock = api.MapGroup("/stock").WithTags("Estoque");
        stock.MapGet("/movements", async (StockService service, Guid? productId, StockMovementType? type, DateOnly? from, DateOnly? to,
                int? page, int? pageSize, CancellationToken ct) =>
            {
                var (p, size) = Paging(page, pageSize);
                var result = await service.GetMovementsAsync(new StockMovementQuery
                {
                    Page = p - 1, PageSize = size, ProductId = productId, Type = type, From = from, To = to,
                }, ct);
                return TypedResults.Ok(new ApiPage<StockMovementDto>(result.Items, p, size, result.TotalCount));
            })
            .WithName("ListStockMovements").WithSummary("Movimentações de estoque (entradas, saídas, ajustes e devoluções)");
        stock.MapGet("/low", async (StockService service, CancellationToken ct) => TypedResults.Ok(await service.GetLowStockAsync(100, ct)))
            .WithName("ListLowStock").WithSummary("Produtos abaixo do estoque mínimo");

        var customers = api.MapGroup("/customers").WithTags("Clientes");
        customers.MapGet("/", async (CustomerService service, string? search, CancellationToken ct)
                => TypedResults.Ok(await service.SearchAsync(search, 50, ct)))
            .WithName("SearchCustomers").WithSummary("Busca clientes por nome, e-mail ou documento (para criar pedidos)");

        var orders = api.MapGroup("/orders").WithTags("Pedidos");
        orders.MapGet("/", async (SalesOrderService service, SalesOrderStatus? status, DateOnly? from, DateOnly? to, Guid? customerId,
                int? page, int? pageSize, CancellationToken ct) =>
            {
                var (p, size) = Paging(page, pageSize);
                var result = await service.GetPagedAsync(new SalesOrderQuery
                {
                    Page = p - 1, PageSize = size, Status = status, From = from, To = to, CustomerId = customerId,
                }, ct);
                return TypedResults.Ok(new ApiPage<SalesOrderListItem>(result.Items, p, size, result.TotalCount));
            })
            .WithName("ListOrders").WithSummary("Lista pedidos com filtros por status, período e cliente");
        orders.MapGet("/{id:guid}", async (Guid id, SalesOrderService service, CancellationToken ct) => TypedResults.Ok(await service.GetAsync(id, ct)))
            .WithName("GetOrder").WithSummary("Detalhes do pedido com itens").ProducesProblem(StatusCodes.Status404NotFound);
        orders.MapPost("/", CreateOrderAsync)
            .WithName("CreateOrder")
            .WithSummary("Cria um pedido (rascunho). Com confirm=true, confirma e baixa o estoque na mesma chamada")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status409Conflict);
        orders.MapPost("/{id:guid}/confirm", async (Guid id, SalesOrderService service, CancellationToken ct) =>
            {
                await service.ConfirmAsync(id, ct);
                return TypedResults.Ok(await service.GetAsync(id, ct));
            })
            .WithName("ConfirmOrder").WithSummary("Confirma o pedido (baixa o estoque)").ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        orders.MapPost("/{id:guid}/cancel", async (Guid id, CancelOrderRequest body, SalesOrderService service, CancellationToken ct) =>
            {
                await service.CancelAsync(id, body.Reason, ct);
                return TypedResults.Ok(await service.GetAsync(id, ct));
            })
            .WithName("CancelOrder").WithSummary("Cancela o pedido (devolve o estoque se já confirmado)").ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<Created<SalesOrderDetails>> CreateOrderAsync(CreateOrderRequest body, SalesOrderService service, CancellationToken ct)
    {
        var id = await service.CreateDraftAsync(new SalesOrderInput
        {
            CustomerId = body.CustomerId,
            PaymentMethod = body.PaymentMethod,
            Discount = body.Discount,
            Notes = body.Notes,
            Items = body.Items?.Select(i => new SalesOrderItemInput { ProductId = i.ProductId, Quantity = i.Quantity, Discount = i.Discount }).ToList() ?? [],
        }, ct);
        if (body.Confirm) await service.ConfirmAsync(id, ct);
        return TypedResults.Created($"/api/v1/orders/{id}", await service.GetAsync(id, ct));
    }

    private static (int Page, int PageSize) Paging(int? page, int? pageSize) => (Math.Max(page ?? 1, 1), Math.Clamp(pageSize ?? 20, 1, 100));
}
