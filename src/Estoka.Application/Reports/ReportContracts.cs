using Estoka.Domain.Catalog;
using Estoka.Domain.Sales;

namespace Estoka.Application.Reports;

public enum ReportKind { Vendas, CurvaAbc, Estoque }

public sealed record ReportPeriod(DateOnly From, DateOnly To)
{
    public override string ToString() => $"{From:dd/MM/yyyy} a {To:dd/MM/yyyy}";
}

public sealed record SalesReportRow(DateOnly Date, int Orders, decimal Gross, decimal Discounts, decimal Net, decimal Cost)
{
    public decimal Margin => Net - Cost;
}

public sealed record PaymentBreakdown(PaymentMethod Method, int Orders, decimal Total);

public sealed record SalesReport(ReportPeriod Period, IReadOnlyList<SalesReportRow> Days, IReadOnlyList<PaymentBreakdown> ByPayment,
    bool IncludesCost)
{
    public int Orders => Days.Sum(d => d.Orders);
    public decimal Net => Days.Sum(d => d.Net);
    public decimal Gross => Days.Sum(d => d.Gross);
    public decimal Discounts => Days.Sum(d => d.Discounts);
    public decimal Cost => Days.Sum(d => d.Cost);
    public decimal AverageTicket => Orders == 0 ? 0 : Math.Round(Net / Orders, 2);
}

public sealed record AbcRow(int Rank, string Sku, string Name, string Category, decimal Quantity, decimal Revenue, decimal SharePercent,
    decimal CumulativePercent, char Class);

public sealed record AbcReport(ReportPeriod Period, IReadOnlyList<AbcRow> Rows)
{
    public decimal Revenue => Rows.Sum(r => r.Revenue);
    public int Count(char c) => Rows.Count(r => r.Class == c);
    public decimal RevenueOf(char c) => Rows.Where(r => r.Class == c).Sum(r => r.Revenue);
}

public sealed record StockPositionRow(string Sku, string Name, string Category, UnitOfMeasure Unit, decimal CurrentStock, decimal MinimumStock,
    decimal? UnitCost, decimal SalePrice, bool IsActive)
{
    public decimal? CostValue => UnitCost is { } c ? Math.Round(c * CurrentStock, 2) : null;
    public decimal SaleValue => Math.Round(SalePrice * CurrentStock, 2);
    public bool IsBelowMinimum => CurrentStock < MinimumStock;
}

public sealed record StockPositionReport(DateTimeOffset GeneratedAt, IReadOnlyList<StockPositionRow> Rows, bool IncludesCost)
{
    public decimal? CostValue => IncludesCost ? Rows.Sum(r => r.CostValue ?? 0) : null;
    public decimal SaleValue => Rows.Sum(r => r.SaleValue);
    public int BelowMinimum => Rows.Count(r => r.IsActive && r.IsBelowMinimum);
}

/// <summary>Gera arquivos Excel (ClosedXML) e PDF (QuestPDF) dos relatórios.</summary>
public interface IReportExporter
{
    byte[] ToExcel(SalesReport report);
    byte[] ToExcel(AbcReport report);
    byte[] ToExcel(StockPositionReport report);
    byte[] ToPdf(SalesReport report, Sales.CompanyInfo company);
    byte[] ToPdf(AbcReport report, Sales.CompanyInfo company);
    byte[] ToPdf(StockPositionReport report, Sales.CompanyInfo company);
}
