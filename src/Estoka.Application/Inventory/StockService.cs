using Estoka.Application.Abstractions;
using Estoka.Application.Common;
using Estoka.Application.Security;
using Estoka.Domain.Common;
using Estoka.Domain.Inventory;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Estoka.Application.Inventory;

public sealed class StockService(
    IAppDbContextFactory dbFactory,
    ICurrentUser user,
    TimeProvider clock,
    IValidator<StockEntryInput> entryValidator,
    IValidator<StockAdjustmentInput> adjustmentValidator)
{
    public async Task<PagedResult<StockMovementDto>> GetMovementsAsync(StockMovementQuery query, CancellationToken ct = default)
    {
        user.Demand(Permission.ViewStock);
        var canViewCost = user.Can(Permission.ViewCost);
        await using var db = await dbFactory.CreateAsync(ct);

        // IgnoreQueryFilters: o histórico continua visível mesmo para produtos excluídos.
        var q = db.StockMovements.AsNoTracking().IgnoreQueryFilters();
        if (query.ProductId is { } productId) q = q.Where(m => m.ProductId == productId);
        if (query.Type is { } type) q = q.Where(m => m.Type == type);
        if (query.From is { } from) q = q.Where(m => m.OccurredAt >= BrazilTime.StartOfDayUtc(from));
        if (query.To is { } to) q = q.Where(m => m.OccurredAt < BrazilTime.StartOfDayUtc(to.AddDays(1)));
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var like = Text.Like(query.Search);
            q = q.Where(m => EF.Functions.ILike(m.Product!.Name, like) || EF.Functions.ILike(m.Product!.Sku, like)
                             || EF.Functions.ILike(m.Reason, like));
        }

        var total = await q.CountAsync(ct);
        q = (query.SortBy, query.SortDescending) switch
        {
            ("OccurredAt", false) => q.OrderBy(m => m.OccurredAt),
            ("ProductName", false) => q.OrderBy(m => m.Product!.Name).ThenByDescending(m => m.OccurredAt),
            ("ProductName", true) => q.OrderByDescending(m => m.Product!.Name).ThenByDescending(m => m.OccurredAt),
            ("Quantity", false) => q.OrderBy(m => m.Quantity),
            ("Quantity", true) => q.OrderByDescending(m => m.Quantity),
            _ => q.OrderByDescending(m => m.OccurredAt).ThenByDescending(m => m.Id),
        };

        var items = await q.Page(query)
            .Select(m => new StockMovementDto(m.Id, m.OccurredAt, m.ProductId, m.Product!.Sku, m.Product.Name, m.Type,
                m.Quantity, m.BalanceAfter, canViewCost ? m.UnitCost : null, m.Reason, m.SalesOrderId,
                m.SalesOrder != null ? m.SalesOrder.Number : null, m.UserName, m.Product.Unit))
            .ToListAsync(ct);
        return new PagedResult<StockMovementDto>(items, total);
    }

    public async Task<IReadOnlyList<LowStockItem>> GetLowStockAsync(int take = 50, CancellationToken ct = default)
    {
        user.Demand(Permission.ViewStock);
        await using var db = await dbFactory.CreateAsync(ct);
        return await db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.CurrentStock < p.MinimumStock)
            .OrderBy(p => p.MinimumStock == 0 ? 1 : p.CurrentStock / p.MinimumStock)
            .ThenBy(p => p.Name)
            .Take(take)
            .Select(p => new LowStockItem(p.Id, p.Sku, p.Name, p.Category!.Name, p.CurrentStock, p.MinimumStock, p.Unit))
            .ToListAsync(ct);
    }

    public async Task<Guid> RegisterEntryAsync(StockEntryInput input, CancellationToken ct = default)
    {
        user.Demand(Permission.ManageStock);
        await entryValidator.EnsureValidAsync(input, ct);
        await using var db = await dbFactory.CreateAsync(ct);
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == input.ProductId, ct) ?? throw NotFoundException.For("Produto");
        var movement = product.RegisterMovement(StockMovementType.Entrada, input.Quantity, input.Reason, user.UserName,
            clock.GetUtcNow(), input.UnitCost);
        db.StockMovements.Add(movement);
        await db.SaveChangesAsync(ct);
        EstokaTelemetry.StockMovements.Add(1);
        return movement.Id;
    }

    public async Task<Guid> RegisterAdjustmentAsync(StockAdjustmentInput input, CancellationToken ct = default)
    {
        user.Demand(Permission.ManageStock);
        await adjustmentValidator.EnsureValidAsync(input, ct);
        await using var db = await dbFactory.CreateAsync(ct);
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == input.ProductId, ct) ?? throw NotFoundException.For("Produto");
        var delta = input.CountedQuantity - product.CurrentStock;
        if (delta == 0) throw new DomainException("A quantidade contada é igual ao saldo atual — nada a ajustar.");
        var movement = product.RegisterMovement(StockMovementType.Ajuste, delta, input.Reason, user.UserName, clock.GetUtcNow());
        db.StockMovements.Add(movement);
        await db.SaveChangesAsync(ct);
        EstokaTelemetry.StockMovements.Add(1);
        return movement.Id;
    }
}
