using Estoka.Application.Abstractions;
using Estoka.Application.Common;
using Estoka.Application.Inventory;
using Estoka.Application.Security;
using Estoka.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace Estoka.Application.Dashboard;

public sealed record DashboardKpis(
    decimal MonthRevenue, decimal PreviousMonthRevenue, int MonthOrders, int PreviousMonthOrders,
    decimal AverageTicket, decimal PreviousAverageTicket, int LowStockCount, int OpenOrders)
{
    public decimal? RevenueChange => Change(MonthRevenue, PreviousMonthRevenue);
    public decimal? OrdersChange => Change(MonthOrders, PreviousMonthOrders);
    public decimal? TicketChange => Change(AverageTicket, PreviousAverageTicket);

    private static decimal? Change(decimal current, decimal previous)
        => previous == 0 ? null : Math.Round((current - previous) / previous * 100, 1);
}

public sealed record DailySales(DateOnly Date, decimal Total, int Orders);
public sealed record ProductRanking(Guid ProductId, string Name, string Sku, decimal Quantity, decimal Revenue);
public sealed record CategorySales(string Category, decimal Revenue);
public sealed record RecentOrder(Guid Id, long Number, string CustomerName, DateTimeOffset OrderDate, SalesOrderStatus Status, decimal Total);

public sealed record DashboardData(
    DashboardKpis Kpis,
    IReadOnlyList<DailySales> Last30Days,
    IReadOnlyList<ProductRanking> TopProducts,
    IReadOnlyList<CategorySales> SalesByCategory,
    IReadOnlyList<LowStockItem> LowStock,
    IReadOnlyList<RecentOrder> RecentOrders,
    DateOnly Today);

/// <summary>Indicadores do painel. Vendas = pedidos confirmados, faturados ou entregues (rascunhos e cancelados não contam).</summary>
public sealed class DashboardService(IAppDbContextFactory dbFactory, ICurrentUser user, TimeProvider clock)
{
    public static readonly SalesOrderStatus[] SoldStatuses =
        [SalesOrderStatus.Confirmado, SalesOrderStatus.Faturado, SalesOrderStatus.Entregue];

    public async Task<DashboardData> GetAsync(CancellationToken ct = default)
    {
        user.Demand(Permission.ViewDashboard);
        await using var db = await dbFactory.CreateAsync(ct);

        var today = BrazilTime.Today(clock);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var previousMonthStart = monthStart.AddMonths(-1);
        // Comparação justa: mesmo número de dias do mês anterior (ex.: 1–4/out vs. 1–4/nov).
        var previousMonthEnd = previousMonthStart.AddDays(Math.Min(today.Day, DateTime.DaysInMonth(previousMonthStart.Year, previousMonthStart.Month)));
        var start30 = today.AddDays(-29);

        var fromUtc = BrazilTime.StartOfDayUtc(new[] { previousMonthStart, start30 }.Min());
        var sold = db.SalesOrders.AsNoTracking().IgnoreQueryFilters().Where(o => SoldStatuses.Contains(o.Status));

        var orders = await sold.Where(o => o.OrderDate >= fromUtc)
            .Select(o => new { o.OrderDate, o.Total })
            .ToListAsync(ct);
        var withDate = orders.Select(o => (Date: BrazilTime.DateOf(o.OrderDate), o.Total)).ToList();

        var month = withDate.Where(o => o.Date >= monthStart).ToList();
        var previous = withDate.Where(o => o.Date >= previousMonthStart && o.Date < previousMonthEnd).ToList();
        var lowStockCount = await db.Products.CountAsync(p => p.IsActive && p.CurrentStock < p.MinimumStock, ct);
        var openOrders = await db.SalesOrders.CountAsync(o => o.Status == SalesOrderStatus.Rascunho || o.Status == SalesOrderStatus.Confirmado
                                                              || o.Status == SalesOrderStatus.Faturado, ct);

        var kpis = new DashboardKpis(
            month.Sum(o => o.Total), previous.Sum(o => o.Total), month.Count, previous.Count,
            month.Count == 0 ? 0 : Math.Round(month.Sum(o => o.Total) / month.Count, 2),
            previous.Count == 0 ? 0 : Math.Round(previous.Sum(o => o.Total) / previous.Count, 2),
            lowStockCount, openOrders);

        var byDay = withDate.Where(o => o.Date >= start30).GroupBy(o => o.Date).ToDictionary(g => g.Key, g => (Total: g.Sum(x => x.Total), Count: g.Count()));
        var daily = Enumerable.Range(0, 30).Select(i => start30.AddDays(i))
            .Select(d => byDay.TryGetValue(d, out var v) ? new DailySales(d, v.Total, v.Count) : new DailySales(d, 0, 0))
            .ToList();

        var start30Utc = BrazilTime.StartOfDayUtc(start30);
        var items30 = db.SalesOrderItems.AsNoTracking().IgnoreQueryFilters()
            .Where(i => db.SalesOrders.Any(o => o.Id == i.SalesOrderId && SoldStatuses.Contains(o.Status) && o.OrderDate >= start30Utc));

        var top = (await items30
            .GroupBy(i => new { i.ProductId, i.ProductName, i.Sku })
            .Select(g => new { g.Key.ProductId, g.Key.ProductName, g.Key.Sku, Quantity = g.Sum(i => i.Quantity), Revenue = g.Sum(i => i.Total) })
            .OrderByDescending(r => r.Revenue)
            .Take(5)
            .ToListAsync(ct))
            .Select(r => new ProductRanking(r.ProductId, r.ProductName, r.Sku, r.Quantity, r.Revenue))
            .ToList();

        var byCategory = (await items30
            .GroupBy(i => i.Product!.Category!.Name)
            .Select(g => new { Category = g.Key, Revenue = g.Sum(i => i.Total) })
            .OrderByDescending(c => c.Revenue)
            .ToListAsync(ct))
            .Select(c => new CategorySales(c.Category, c.Revenue))
            .ToList();

        var lowStock = await db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.CurrentStock < p.MinimumStock)
            .OrderBy(p => p.MinimumStock == 0 ? 1 : p.CurrentStock / p.MinimumStock).ThenBy(p => p.Name)
            .Take(8)
            .Select(p => new LowStockItem(p.Id, p.Sku, p.Name, p.Category!.Name, p.CurrentStock, p.MinimumStock, p.Unit))
            .ToListAsync(ct);

        var recent = await db.SalesOrders.AsNoTracking().IgnoreQueryFilters()
            .OrderByDescending(o => o.OrderDate).Take(6)
            .Select(o => new RecentOrder(o.Id, o.Number, o.Customer!.Name, o.OrderDate, o.Status, o.Total))
            .ToListAsync(ct);

        return new DashboardData(kpis, daily, top, byCategory, lowStock, recent, today);
    }
}
