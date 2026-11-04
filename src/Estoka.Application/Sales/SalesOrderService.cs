using Estoka.Application.Abstractions;
using Estoka.Application.Common;
using Estoka.Application.Security;
using Estoka.Domain.Catalog;
using Estoka.Domain.Common;
using Estoka.Domain.Inventory;
using Estoka.Domain.Sales;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Estoka.Application.Sales;

public sealed class SalesOrderService(
    IAppDbContextFactory dbFactory, ICurrentUser user, TimeProvider clock, IValidator<SalesOrderInput> validator)
{
    /// <summary>Tentativas ao confirmar/cancelar quando outro pedido alterou o mesmo produto ao mesmo tempo.</summary>
    internal const int MaxConcurrencyAttempts = 3;

    public async Task<PagedResult<SalesOrderListItem>> GetPagedAsync(SalesOrderQuery query, CancellationToken ct = default)
    {
        user.Demand(Permission.ViewOrders);
        await using var db = await dbFactory.CreateAsync(ct);

        // IgnoreQueryFilters: pedidos de clientes excluídos (soft delete) continuam visíveis.
        var q = db.SalesOrders.AsNoTracking().IgnoreQueryFilters();
        if (query.Status is { } status) q = q.Where(o => o.Status == status);
        if (query.CustomerId is { } customerId) q = q.Where(o => o.CustomerId == customerId);
        if (query.From is { } from)
        {
            var start = BrazilTime.StartOfDayUtc(from);
            q = q.Where(o => o.OrderDate >= start);
        }
        if (query.To is { } to)
        {
            var end = BrazilTime.StartOfDayUtc(to.AddDays(1));
            q = q.Where(o => o.OrderDate < end);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            var numberText = new string(term.Where(char.IsAsciiDigit).ToArray());
            long? number = numberText.Length is > 0 and < 12 ? long.Parse(numberText, System.Globalization.CultureInfo.InvariantCulture) : null;
            var like = Text.Like(term);
            q = q.Where(o => (number != null && o.Number == number) || EF.Functions.ILike(o.Customer!.Name, like));
        }

        var total = await q.CountAsync(ct);
        q = (query.SortBy, query.SortDescending) switch
        {
            ("Number", false) => q.OrderBy(o => o.Number),
            ("Number", true) => q.OrderByDescending(o => o.Number),
            ("CustomerName", false) => q.OrderBy(o => o.Customer!.Name),
            ("CustomerName", true) => q.OrderByDescending(o => o.Customer!.Name),
            ("Total", false) => q.OrderBy(o => o.Total),
            ("Total", true) => q.OrderByDescending(o => o.Total),
            ("Status", false) => q.OrderBy(o => o.Status),
            ("Status", true) => q.OrderByDescending(o => o.Status),
            ("OrderDate", false) => q.OrderBy(o => o.OrderDate),
            _ => q.OrderByDescending(o => o.OrderDate).ThenByDescending(o => o.Number),
        };

        var items = await q.Page(query)
            .Select(o => new SalesOrderListItem(o.Id, o.Number, o.OrderDate, o.CustomerId, o.Customer!.Name, o.Status,
                o.PaymentMethod, o.Items.Count, o.Total, o.SellerName))
            .ToListAsync(ct);
        return new PagedResult<SalesOrderListItem>(items, total);
    }

    public async Task<SalesOrderDetails> GetAsync(Guid id, CancellationToken ct = default)
    {
        user.Demand(Permission.ViewOrders);
        await using var db = await dbFactory.CreateAsync(ct);
        var o = await db.SalesOrders.AsNoTracking().IgnoreQueryFilters()
            .Include(x => x.Customer)
            .Include(x => x.Items).ThenInclude(i => i.Product)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Pedido");
        var c = o.Customer!;
        return new SalesOrderDetails(o.Id, o.Number, o.OrderDate, o.Status, o.PaymentMethod,
            new SalesOrderCustomer(c.Id, c.Name, c.Document, c.Type, c.Email, c.Phone, c.Address),
            o.SellerName, o.Notes, o.Subtotal, o.Discount, o.Total,
            o.Items.OrderBy(i => i.ProductName)
                .Select(i => new SalesOrderItemDto(i.Id, i.ProductId, i.Sku, i.ProductName, i.Product?.Unit ?? UnitOfMeasure.Unidade,
                    i.Quantity, i.UnitPrice, i.Discount, i.Total, i.Product?.CurrentStock ?? 0))
                .ToList(),
            o.ConfirmedAt, o.InvoicedAt, o.DeliveredAt, o.CanceledAt, o.CancelReason);
    }

    /// <summary>Cria um pedido em rascunho. Não mexe no estoque.</summary>
    public async Task<Guid> CreateDraftAsync(SalesOrderInput input, CancellationToken ct = default)
    {
        user.Demand(Permission.ManageOrders);
        await validator.EnsureValidAsync(input, ct);
        await using var db = await dbFactory.CreateAsync(ct);
        await EnsureCustomerAsync(db, input.CustomerId, ct);

        var order = new SalesOrder(input.CustomerId, clock.GetUtcNow(), input.PaymentMethod, user.UserName, input.Notes);
        await FillItemsAsync(db, order, input, ct);
        db.SalesOrders.Add(order);
        await db.SaveChangesAsync(ct);
        return order.Id;
    }

    /// <summary>Substitui cabeçalho e itens de um pedido em rascunho.</summary>
    public async Task UpdateDraftAsync(Guid id, SalesOrderInput input, CancellationToken ct = default)
    {
        user.Demand(Permission.ManageOrders);
        await validator.EnsureValidAsync(input, ct);
        await using var db = await dbFactory.CreateAsync(ct);
        var order = await db.SalesOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct)
                    ?? throw NotFoundException.For("Pedido");
        if (!order.IsEditable) throw new DomainException("Somente pedidos em rascunho podem ser alterados.");
        await EnsureCustomerAsync(db, input.CustomerId, ct);

        order.ClearItems();
        await FillItemsAsync(db, order, input, ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Confirma o pedido: baixa o estoque de todos os itens (movimentações de SAÍDA) numa única transação.
    /// Se outro usuário alterar um dos produtos ao mesmo tempo (xmin diferente), a operação é refeita
    /// do zero com dados atualizados; se o estoque não for mais suficiente, a confirmação falha com mensagem amigável.
    /// </summary>
    public Task ConfirmAsync(Guid id, CancellationToken ct = default)
        => ExecuteWithConcurrencyRetryAsync(id, (db, order, products, now) =>
        {
            order.Confirm(now);
            foreach (var item in order.Items)
            {
                var product = products[item.ProductId];
                if (product.IsDeleted || !product.IsActive)
                    throw new DomainException($"O produto \"{item.ProductName}\" não está mais disponível para venda.");
                db.StockMovements.Add(product.RegisterMovement(StockMovementType.Saida, item.Quantity,
                    $"Venda {order.Code}", user.UserName, now, salesOrderId: order.Id));
            }
        }, ct);

    public Task InvoiceAsync(Guid id, CancellationToken ct = default)
        => ExecuteWithConcurrencyRetryAsync(id, (_, order, _, now) => order.Invoice(now), ct);

    public Task DeliverAsync(Guid id, CancellationToken ct = default)
        => ExecuteWithConcurrencyRetryAsync(id, (_, order, _, now) => order.Deliver(now), ct);

    /// <summary>Cancela o pedido; se o estoque já havia sido baixado, devolve via movimentações de DEVOLUÇÃO.</summary>
    public Task CancelAsync(Guid id, string reason, CancellationToken ct = default)
        => ExecuteWithConcurrencyRetryAsync(id, (db, order, products, now) =>
        {
            if (!order.Cancel(now, string.IsNullOrWhiteSpace(reason) ? "Cancelado pelo usuário" : reason)) return;
            foreach (var item in order.Items)
                db.StockMovements.Add(products[item.ProductId].RegisterMovement(StockMovementType.Devolucao, item.Quantity,
                    $"Cancelamento {order.Code}", user.UserName, now, salesOrderId: order.Id));
        }, ct);

    private async Task ExecuteWithConcurrencyRetryAsync(
        Guid id, Action<IAppDbContext, SalesOrder, IReadOnlyDictionary<Guid, Product>, DateTimeOffset> action, CancellationToken ct)
    {
        user.Demand(Permission.ManageOrders);
        for (var attempt = 1; ; attempt++)
        {
            await using var db = await dbFactory.CreateAsync(ct);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var order = await db.SalesOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct)
                            ?? throw NotFoundException.For("Pedido");
                var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();
                var products = await db.Products.IgnoreQueryFilters()
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, ct);

                action(db, order, products, clock.GetUtcNow());
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return;
            }
            catch (ConcurrencyConflictException) when (attempt < MaxConcurrencyAttempts)
            {
                await tx.RollbackAsync(ct);
                await Task.Delay(Random.Shared.Next(20, 80) * attempt, ct);
            }
        }
    }

    private async Task FillItemsAsync(IAppDbContext db, SalesOrder order, SalesOrderInput input, CancellationToken ct)
    {
        var ids = input.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var canChangePrice = user.Can(Permission.ViewCost);
        foreach (var item in input.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
                throw new DomainException("Um dos produtos do pedido não foi encontrado.");
            var price = canChangePrice && item.UnitPrice is { } p ? p : product.SalePrice;
            order.AddItem(product, item.Quantity, price, item.Discount);
        }
        order.SetDiscount(input.Discount);
    }

    private static async Task EnsureCustomerAsync(IAppDbContext db, Guid customerId, CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == customerId, ct))
            throw new DomainException("Cliente não encontrado.");
    }
}
