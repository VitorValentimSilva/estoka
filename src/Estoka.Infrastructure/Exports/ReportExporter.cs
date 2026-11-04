using ClosedXML.Excel;
using Estoka.Application.Common;
using Estoka.Application.Reports;
using Estoka.Application.Sales;
using Estoka.Domain.Catalog;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static Estoka.Infrastructure.Exports.PdfStyles;

namespace Estoka.Infrastructure.Exports;

internal sealed class ReportExporter(TimeProvider clock) : IReportExporter
{
    private const string MoneyFormat = "\"R$\" #,##0.00";
    private const string QtyFormat = "#,##0.###";
    private const string PercentFormat = "0.00\"%\"";

    // ===================== Excel =====================

    public byte[] ToExcel(SalesReport r)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Vendas por dia");
        var headers = new List<string> { "Data", "Pedidos", "Bruto", "Descontos", "Líquido" };
        if (r.IncludesCost) headers.AddRange(["Custo", "Margem bruta"]);
        var row = WriteTitle(ws, "Vendas por período", r.Period.ToString(), headers);
        foreach (var d in r.Days)
        {
            ws.Cell(row, 1).Value = d.Date.ToDateTime(TimeOnly.MinValue);
            ws.Cell(row, 2).Value = d.Orders;
            ws.Cell(row, 3).Value = d.Gross;
            ws.Cell(row, 4).Value = d.Discounts;
            ws.Cell(row, 5).Value = d.Net;
            if (r.IncludesCost)
            {
                ws.Cell(row, 6).Value = d.Cost;
                ws.Cell(row, 7).Value = d.Margin;
            }
            row++;
        }
        var totalRow = row;
        ws.Cell(row, 1).Value = "Total";
        ws.Cell(row, 2).FormulaA1 = $"SUM(B5:B{row - 1})";
        for (var c = 3; c <= headers.Count; c++)
            ws.Cell(row, c).FormulaA1 = $"SUM({Col(c)}5:{Col(c)}{row - 1})";
        ws.Row(totalRow).Style.Font.Bold = true;
        ws.Range(5, 1, totalRow, 1).Style.NumberFormat.Format = "dd/mm/yyyy";
        ws.Range(5, 3, totalRow, headers.Count).Style.NumberFormat.Format = MoneyFormat;
        FinishTable(ws, headers.Count, totalRow);

        var pay = wb.AddWorksheet("Por forma de pagamento");
        var prow = WriteTitle(pay, "Vendas por forma de pagamento", r.Period.ToString(), ["Forma de pagamento", "Pedidos", "Total"]);
        foreach (var p in r.ByPayment)
        {
            pay.Cell(prow, 1).Value = p.Method.Label();
            pay.Cell(prow, 2).Value = p.Orders;
            pay.Cell(prow, 3).Value = p.Total;
            prow++;
        }
        pay.Range(5, 3, prow, 3).Style.NumberFormat.Format = MoneyFormat;
        FinishTable(pay, 3, prow - 1);
        return Save(wb);
    }

    public byte[] ToExcel(AbcReport r)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Curva ABC");
        var row = WriteTitle(ws, "Curva ABC de produtos (faturamento)", r.Period.ToString(),
            ["#", "SKU", "Produto", "Categoria", "Quantidade", "Faturamento", "% do total", "% acumulado", "Classe"]);
        foreach (var a in r.Rows)
        {
            ws.Cell(row, 1).Value = a.Rank;
            ws.Cell(row, 2).Value = a.Sku;
            ws.Cell(row, 3).Value = a.Name;
            ws.Cell(row, 4).Value = a.Category;
            ws.Cell(row, 5).Value = a.Quantity;
            ws.Cell(row, 6).Value = a.Revenue;
            ws.Cell(row, 7).Value = a.SharePercent;
            ws.Cell(row, 8).Value = a.CumulativePercent;
            ws.Cell(row, 9).Value = a.Class.ToString();
            ws.Cell(row, 9).Style.Fill.BackgroundColor = a.Class switch
            {
                'A' => XLColor.FromHtml("#DCFCE7"), 'B' => XLColor.FromHtml("#FEF3C7"), _ => XLColor.FromHtml("#FEE2E2"),
            };
            row++;
        }
        ws.Range(5, 5, row, 5).Style.NumberFormat.Format = QtyFormat;
        ws.Range(5, 6, row, 6).Style.NumberFormat.Format = MoneyFormat;
        ws.Range(5, 7, row, 8).Style.NumberFormat.Format = PercentFormat;
        ws.Column(9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        FinishTable(ws, 9, row - 1);
        return Save(wb);
    }

    public byte[] ToExcel(StockPositionReport r)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Posição de estoque");
        var headers = new List<string> { "SKU", "Produto", "Categoria", "Unidade", "Saldo", "Mínimo", "Situação", "Preço de venda", "Valor (venda)" };
        if (r.IncludesCost) headers.AddRange(["Custo unitário", "Valor (custo)"]);
        var row = WriteTitle(ws, "Posição de estoque", $"Gerado em {BrazilTime.ToLocal(r.GeneratedAt):dd/MM/yyyy HH:mm}", headers);
        foreach (var p in r.Rows)
        {
            ws.Cell(row, 1).Value = p.Sku;
            ws.Cell(row, 2).Value = p.Name;
            ws.Cell(row, 3).Value = p.Category;
            ws.Cell(row, 4).Value = p.Unit.Abbreviation();
            ws.Cell(row, 5).Value = p.CurrentStock;
            ws.Cell(row, 6).Value = p.MinimumStock;
            ws.Cell(row, 7).Value = !p.IsActive ? "Inativo" : p.IsBelowMinimum ? "Abaixo do mínimo" : "OK";
            if (p.IsActive && p.IsBelowMinimum) ws.Cell(row, 7).Style.Font.FontColor = XLColor.FromHtml("#B45309");
            ws.Cell(row, 8).Value = p.SalePrice;
            ws.Cell(row, 9).Value = p.SaleValue;
            if (r.IncludesCost)
            {
                ws.Cell(row, 10).Value = p.UnitCost ?? 0;
                ws.Cell(row, 11).Value = p.CostValue ?? 0;
            }
            row++;
        }
        ws.Cell(row, 1).Value = "Total";
        ws.Cell(row, 9).FormulaA1 = $"SUM(I5:I{row - 1})";
        if (r.IncludesCost) ws.Cell(row, 11).FormulaA1 = $"SUM(K5:K{row - 1})";
        ws.Row(row).Style.Font.Bold = true;
        ws.Range(5, 5, row, 6).Style.NumberFormat.Format = QtyFormat;
        ws.Range(5, 8, row, headers.Count).Style.NumberFormat.Format = MoneyFormat;
        FinishTable(ws, headers.Count, row);
        return Save(wb);
    }

    private static int WriteTitle(IXLWorksheet ws, string title, string subtitle, IReadOnlyList<string> headers)
    {
        ws.Cell(1, 1).Value = title;
        ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14).Font.SetFontColor(XLColor.FromHtml(Primary));
        ws.Cell(2, 1).Value = subtitle;
        ws.Cell(2, 1).Style.Font.SetFontColor(XLColor.FromHtml(Muted));
        for (var i = 0; i < headers.Count; i++)
        {
            var cell = ws.Cell(4, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(Primary);
        }
        return 5;
    }

    private static void FinishTable(IXLWorksheet ws, int columns, int lastRow)
    {
        if (lastRow >= 5) ws.Range(4, 1, lastRow, columns).SetAutoFilter();
        ws.SheetView.FreezeRows(4);
        ws.Columns(1, columns).AdjustToContents(4, lastRow);
        foreach (var col in ws.Columns(1, columns)) if (col.Width > 60) col.Width = 60;
    }

    private static string Col(int index) => XLHelper.GetColumnLetterFromNumber(index);

    private static byte[] Save(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ===================== PDF =====================

    public byte[] ToPdf(SalesReport r, CompanyInfo company) => Pdf(company, "Vendas por período", r.Period.ToString(), PageSizes.A4, col =>
    {
        col.Item().Row(row =>
        {
            row.Spacing(8);
            Stat(row.RelativeItem(), "Faturamento líquido", Money(r.Net));
            Stat(row.RelativeItem(), "Pedidos", r.Orders.ToString("N0", PtBr));
            Stat(row.RelativeItem(), "Ticket médio", Money(r.AverageTicket));
            if (r.IncludesCost) Stat(row.RelativeItem(), "Margem bruta", Money(r.Net - r.Cost));
        });
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                c.ConstantColumn(70); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn();
                if (r.IncludesCost) c.RelativeColumn();
            });
            t.Header(h =>
            {
                h.Cell().Element(HeaderCell).Text("DATA");
                h.Cell().Element(HeaderCell).AlignRight().Text("PEDIDOS");
                h.Cell().Element(HeaderCell).AlignRight().Text("BRUTO");
                h.Cell().Element(HeaderCell).AlignRight().Text("DESCONTOS");
                h.Cell().Element(HeaderCell).AlignRight().Text("LÍQUIDO");
                if (r.IncludesCost) h.Cell().Element(HeaderCell).AlignRight().Text("MARGEM");
            });
            var i = 0;
            foreach (var d in r.Days.Where(d => d.Orders > 0))
            {
                var z = i++ % 2 == 1;
                t.Cell().Element(c => BodyCell(c, z)).Text(d.Date.ToString("dd/MM/yyyy", PtBr));
                t.Cell().Element(c => BodyCell(c, z)).AlignRight().Text(d.Orders.ToString(PtBr));
                t.Cell().Element(c => BodyCell(c, z)).AlignRight().Text(Money(d.Gross));
                t.Cell().Element(c => BodyCell(c, z)).AlignRight().Text(Money(d.Discounts));
                t.Cell().Element(c => BodyCell(c, z)).AlignRight().Text(Money(d.Net)).SemiBold();
                if (r.IncludesCost) t.Cell().Element(c => BodyCell(c, z)).AlignRight().Text(Money(d.Margin));
            }
        });
        col.Item().PaddingTop(6).Text("Por forma de pagamento").SemiBold().FontColor(Primary);
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn(); });
            t.Header(h =>
            {
                h.Cell().Element(HeaderCell).Text("FORMA");
                h.Cell().Element(HeaderCell).AlignRight().Text("PEDIDOS");
                h.Cell().Element(HeaderCell).AlignRight().Text("TOTAL");
            });
            var i = 0;
            foreach (var p in r.ByPayment)
            {
                var z = i++ % 2 == 1;
                t.Cell().Element(c => BodyCell(c, z)).Text(p.Method.Label());
                t.Cell().Element(c => BodyCell(c, z)).AlignRight().Text(p.Orders.ToString(PtBr));
                t.Cell().Element(c => BodyCell(c, z)).AlignRight().Text(Money(p.Total));
            }
        });
    });

    public byte[] ToPdf(AbcReport r, CompanyInfo company) => Pdf(company, "Curva ABC de produtos", r.Period.ToString(), PageSizes.A4.Landscape(), col =>
    {
        col.Item().Row(row =>
        {
            row.Spacing(8);
            foreach (var c in "ABC")
                Stat(row.RelativeItem(), $"Classe {c}", $"{r.Count(c)} produtos · {Money(r.RevenueOf(c))}");
        });
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                c.ConstantColumn(28); c.ConstantColumn(62); c.RelativeColumn(3); c.RelativeColumn(2);
                c.ConstantColumn(64); c.ConstantColumn(84); c.ConstantColumn(56); c.ConstantColumn(64); c.ConstantColumn(44);
            });
            t.Header(h =>
            {
                foreach (var (text, right) in new[] { ("#", false), ("SKU", false), ("PRODUTO", false), ("CATEGORIA", false), ("QTD.", true),
                             ("FATURAMENTO", true), ("% TOTAL", true), ("% ACUM.", true), ("CLASSE", true) })
                {
                    var cell = h.Cell().Element(HeaderCell);
                    (right ? cell.AlignRight() : cell).Text(text);
                }
            });
            var i = 0;
            foreach (var a in r.Rows)
            {
                var z = i++ % 2 == 1;
                t.Cell().Element(c => BodyCell(c, z)).Text(a.Rank.ToString(PtBr)).FontColor(Muted);
                t.Cell().Element(c => BodyCell(c, z)).Text(a.Sku).FontSize(8.5f);
                t.Cell().Element(c => BodyCell(c, z)).Text(a.Name);
                t.Cell().Element(c => BodyCell(c, z)).Text(a.Category).FontColor(Muted);
                t.Cell().Element(c => BodyCell(c, z)).AlignRight().Text(Qty(a.Quantity));
                t.Cell().Element(c => BodyCell(c, z)).AlignRight().Text(Money(a.Revenue));
                t.Cell().Element(c => BodyCell(c, z)).AlignRight().Text($"{a.SharePercent:0.00}%");
                t.Cell().Element(c => BodyCell(c, z)).AlignRight().Text($"{a.CumulativePercent:0.00}%");
                t.Cell().Element(c => BodyCell(c, z)).AlignRight().Text(a.Class.ToString()).Bold()
                    .FontColor(a.Class switch { 'A' => "#15803D", 'B' => "#B45309", _ => "#B91C1C" });
            }
        });
    });

    public byte[] ToPdf(StockPositionReport r, CompanyInfo company) => Pdf(company, "Posição de estoque",
        $"Gerado em {BrazilTime.ToLocal(r.GeneratedAt):dd/MM/yyyy HH:mm}", PageSizes.A4.Landscape(), col =>
    {
        col.Item().Row(row =>
        {
            row.Spacing(8);
            Stat(row.RelativeItem(), "Produtos", r.Rows.Count.ToString(PtBr));
            Stat(row.RelativeItem(), "Abaixo do mínimo", r.BelowMinimum.ToString(PtBr));
            Stat(row.RelativeItem(), "Valor em estoque (venda)", Money(r.SaleValue));
            if (r.CostValue is { } cost) Stat(row.RelativeItem(), "Valor em estoque (custo)", Money(cost));
        });
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                c.ConstantColumn(60); c.RelativeColumn(3); c.RelativeColumn(2); c.ConstantColumn(62); c.ConstantColumn(54);
                c.ConstantColumn(76); c.ConstantColumn(80);
                if (r.IncludesCost) c.ConstantColumn(80);
            });
            t.Header(h =>
            {
                h.Cell().Element(HeaderCell).Text("SKU");
                h.Cell().Element(HeaderCell).Text("PRODUTO");
                h.Cell().Element(HeaderCell).Text("CATEGORIA");
                h.Cell().Element(HeaderCell).AlignRight().Text("SALDO");
                h.Cell().Element(HeaderCell).AlignRight().Text("MÍNIMO");
                h.Cell().Element(HeaderCell).AlignRight().Text("PREÇO");
                h.Cell().Element(HeaderCell).AlignRight().Text("VALOR VENDA");
                if (r.IncludesCost) h.Cell().Element(HeaderCell).AlignRight().Text("VALOR CUSTO");
            });
            var i = 0;
            foreach (var p in r.Rows)
            {
                var z = i++ % 2 == 1;
                var low = p.IsActive && p.IsBelowMinimum;
                t.Cell().Element(c => BodyCell(c, z)).Text(p.Sku).FontSize(8.5f);
                t.Cell().Element(c => BodyCell(c, z)).Text(p.IsActive ? p.Name : $"{p.Name} (inativo)");
                t.Cell().Element(c => BodyCell(c, z)).Text(p.Category).FontColor(Muted);
                t.Cell().Element(c => BodyCell(c, z)).AlignRight().Text($"{Qty(p.CurrentStock)} {p.Unit.Abbreviation()}").FontColor(low ? "#B45309" : "#0F172A");
                t.Cell().Element(c => BodyCell(c, z)).AlignRight().Text(Qty(p.MinimumStock)).FontColor(Muted);
                t.Cell().Element(c => BodyCell(c, z)).AlignRight().Text(Money(p.SalePrice));
                t.Cell().Element(c => BodyCell(c, z)).AlignRight().Text(Money(p.SaleValue));
                if (r.IncludesCost) t.Cell().Element(c => BodyCell(c, z)).AlignRight().Text(Money(p.CostValue ?? 0));
            }
        });
    });

    private byte[] Pdf(CompanyInfo company, string title, string subtitle, PageSize size, Action<ColumnDescriptor> content)
    {
        var now = BrazilTime.ToLocal(clock.GetUtcNow());
        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(size);
            page.Margin(28);
            page.DefaultTextStyle(x => x.FontSize(9).FontColor("#0F172A"));
            page.Header().PaddingBottom(12).Element(c => ComposeHeader(c, company.Name, company.City, title, subtitle));
            page.Content().Column(col =>
            {
                col.Spacing(10);
                content(col);
            });
            page.Footer().Element(c => ComposeFooter(c, now));
        })).GeneratePdf();
    }

    private static void Stat(IContainer container, string label, string value)
        => container.Border(0.5f).BorderColor(Line).Padding(8).Column(c =>
        {
            c.Item().Text(label).FontSize(7.5f).SemiBold().FontColor(Muted);
            c.Item().Text(value).FontSize(12).Bold();
        });
}
