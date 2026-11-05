using Estoka.Application.Abstractions;
using Estoka.Application.Common;
using Estoka.Application.Dashboard;
using Estoka.Application.Security;
using Estoka.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Estoka.Application.Reports;

public sealed class ReportService(IAppDbContextFactory dbFactory, ICurrentUser user, TimeProvider clock)
{
    private const int MaxPeriodDays = 366;

    public ReportPeriod DefaultPeriod()
    {
        var today = BrazilTime.Today(clock);
        return new ReportPeriod(today.AddDays(-29), today);
    }

    public async Task<SalesReport> SalesAsync(ReportPeriod period, CancellationToken ct = default)
    {
        user.Demand(Permission.ViewReports);
        Validate(period);
        var includesCost = user.Can(Permission.ViewCost);
        await using var db = await dbFactory.CreateAsync(ct);
        var (from, to) = Bounds(period);

        var orders = await db.SalesOrders.AsNoTracking().IgnoreQueryFilters()
            .Where(o => DashboardService.SoldStatuses.Contains(o.Status) && o.OrderDate >= from && o.OrderDate < to)
            .Select(o => new
            {
                o.OrderDate, o.PaymentMethod, o.Total, o.Discount,
                ItemDiscounts = o.Items.Sum(i => i.Discount),
                Gross = o.Items.Sum(i => i.Quantity * i.UnitPrice),
                Cost = o.Items.Sum(i => i.Quantity * i.UnitCost),
            })
            .ToListAsync(ct);

        var byDay = orders.GroupBy(o => BrazilTime.DateOf(o.OrderDate)).ToDictionary(g => g.Key);
        var days = new List<SalesReportRow>();
        for (var d = period.From; d <= period.To; d = d.AddDays(1))
        {
            days.Add(byDay.TryGetValue(d, out var g)
                ? new SalesReportRow(d, g.Count(), Math.Round(g.Sum(o => o.Gross), 2), g.Sum(o => o.Discount + o.ItemDiscounts),
                    g.Sum(o => o.Total), includesCost ? Math.Round(g.Sum(o => o.Cost), 2) : 0)
                : new SalesReportRow(d, 0, 0, 0, 0, 0));
        }

        var byPayment = orders.GroupBy(o => o.PaymentMethod)
            .Select(g => new PaymentBreakdown(g.Key, g.Count(), g.Sum(o => o.Total)))
            .OrderByDescending(p => p.Total)
            .ToList();

        return new SalesReport(period, days, byPayment, includesCost);
    }

    /// <summary>Curva ABC por faturamento: A = até 80% acumulado, B = até 95%, C = restante.</summary>
    public async Task<AbcReport> AbcAsync(ReportPeriod period, CancellationToken ct = default)
    {
        user.Demand(Permission.ViewReports);
        Validate(period);
        await using var db = await dbFactory.CreateAsync(ct);
        var (from, to) = Bounds(period);

        var grouped = await db.SalesOrderItems.AsNoTracking().IgnoreQueryFilters()
            .Where(i => db.SalesOrders.Any(o => o.Id == i.SalesOrderId && DashboardService.SoldStatuses.Contains(o.Status)
                                                 && o.OrderDate >= from && o.OrderDate < to))
            .GroupBy(i => new { i.ProductId, i.Sku, i.ProductName, Category = i.Product!.Category!.Name })
            .Select(g => new { g.Key.Sku, g.Key.ProductName, g.Key.Category, Quantity = g.Sum(i => i.Quantity), Revenue = g.Sum(i => i.Total) })
            .OrderByDescending(x => x.Revenue)
            .ToListAsync(ct);

        var total = grouped.Sum(g => g.Revenue);
        var rows = new List<AbcRow>();
        decimal cumulative = 0;
        foreach (var (g, i) in grouped.Select((g, i) => (g, i)))
        {
            var share = total == 0 ? 0 : g.Revenue / total * 100;
            var previous = cumulative;
            cumulative += share;
            // A classe é definida pelo acumulado ANTES do item: o item que cruza 80% ainda é A.
            var cls = previous < 80 ? 'A' : previous < 95 ? 'B' : 'C';
            rows.Add(new AbcRow(i + 1, g.Sku, g.ProductName, g.Category, g.Quantity, g.Revenue, Math.Round(share, 2), Math.Round(cumulative, 2), cls));
        }
        return new AbcReport(period, rows);
    }

    public async Task<StockPositionReport> StockPositionAsync(CancellationToken ct = default)
    {
        user.Demand(Permission.ViewReports);
        var includesCost = user.Can(Permission.ViewCost);
        await using var db = await dbFactory.CreateAsync(ct);
        var rows = await db.Products.AsNoTracking()
            .OrderBy(p => p.Category!.Name).ThenBy(p => p.Name)
            .Select(p => new StockPositionRow(p.Sku, p.Name, p.Category!.Name, p.Unit, p.CurrentStock, p.MinimumStock,
                includesCost ? p.CostPrice : null, p.SalePrice, p.IsActive))
            .ToListAsync(ct);
        return new StockPositionReport(clock.GetUtcNow(), rows, includesCost);
    }

    private static void Validate(ReportPeriod period)
    {
        if (period.To < period.From) throw new DomainException("A data final deve ser maior ou igual à inicial.");
        if (period.To.DayNumber - period.From.DayNumber > MaxPeriodDays) throw new DomainException("O período máximo é de 1 ano.");
    }

    private static (DateTimeOffset From, DateTimeOffset To) Bounds(ReportPeriod p)
        => (BrazilTime.StartOfDayUtc(p.From), BrazilTime.StartOfDayUtc(p.To.AddDays(1)));
}
