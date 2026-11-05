using Estoka.Application.Common;
using Estoka.Application.Sales;
using Estoka.Domain.Catalog;
using Estoka.Domain.Common;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static Estoka.Infrastructure.Exports.PdfStyles;

namespace Estoka.Infrastructure.Exports;

internal sealed class OrderPdfRenderer(TimeProvider clock) : IOrderDocumentRenderer
{
    public byte[] RenderPdf(SalesOrderDetails order, CompanyInfo company)
    {
        var now = BrazilTime.ToLocal(clock.GetUtcNow());
        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(32);
            page.DefaultTextStyle(x => x.FontSize(9.5f).FontColor("#0F172A"));

            page.Header().PaddingBottom(14).Element(c => ComposeHeader(c, company.Name, company.City,
                $"Pedido {order.Code}", $"Emitido em {BrazilTime.ToLocal(order.OrderDate):dd/MM/yyyy HH:mm} · {order.Status.Label()}"));

            page.Content().Column(col =>
            {
                col.Spacing(12);
                col.Item().Row(row =>
                {
                    row.Spacing(12);
                    row.RelativeItem().Border(0.5f).BorderColor(Line).Padding(10).Column(c =>
                    {
                        c.Item().Text("CLIENTE").FontSize(7.5f).SemiBold().FontColor(Muted);
                        c.Item().Text(order.Customer.Name).Bold();
                        c.Item().Text($"{(order.Customer.Type == Domain.Customers.PersonType.PF ? "CPF" : "CNPJ")}: {DocumentValidator.Format(order.Customer.Document)}");
                        var address = order.Customer.Address.ToString();
                        if (address.Length > 0) c.Item().Text(address).FontColor(Muted);
                        var contact = string.Join(" · ", new[] { order.Customer.Email, FormatPhone(order.Customer.Phone) }.Where(s => !string.IsNullOrEmpty(s)));
                        if (contact.Length > 0) c.Item().Text(contact).FontColor(Muted);
                    });
                    row.ConstantItem(190).Border(0.5f).BorderColor(Line).Padding(10).Column(c =>
                    {
                        c.Item().Text("CONDIÇÕES").FontSize(7.5f).SemiBold().FontColor(Muted);
                        c.Item().Text($"Pagamento: {order.PaymentMethod.Label()}");
                        c.Item().Text($"Vendedor: {order.SellerName}");
                        c.Item().Text($"Situação: {order.Status.Label()}");
                    });
                });

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(62);
                        cols.RelativeColumn();
                        cols.ConstantColumn(52);
                        cols.ConstantColumn(66);
                        cols.ConstantColumn(56);
                        cols.ConstantColumn(72);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Element(HeaderCell).Text("SKU");
                        h.Cell().Element(HeaderCell).Text("PRODUTO");
                        h.Cell().Element(HeaderCell).AlignRight().Text("QTD.");
                        h.Cell().Element(HeaderCell).AlignRight().Text("UNITÁRIO");
                        h.Cell().Element(HeaderCell).AlignRight().Text("DESC.");
                        h.Cell().Element(HeaderCell).AlignRight().Text("TOTAL");
                    });
                    var i = 0;
                    foreach (var item in order.Items)
                    {
                        var zebra = i++ % 2 == 1;
                        table.Cell().Element(c => BodyCell(c, zebra)).Text(item.Sku).FontSize(8.5f);
                        table.Cell().Element(c => BodyCell(c, zebra)).Text(item.ProductName);
                        table.Cell().Element(c => BodyCell(c, zebra)).AlignRight().Text($"{Qty(item.Quantity)} {item.Unit.Abbreviation()}");
                        table.Cell().Element(c => BodyCell(c, zebra)).AlignRight().Text(Money(item.UnitPrice));
                        table.Cell().Element(c => BodyCell(c, zebra)).AlignRight().Text(item.Discount > 0 ? Money(item.Discount) : "—");
                        table.Cell().Element(c => BodyCell(c, zebra)).AlignRight().Text(Money(item.Total)).SemiBold();
                    }
                });

                col.Item().AlignRight().Width(220).Column(c =>
                {
                    c.Item().Row(r => { r.RelativeItem().Text("Subtotal").FontColor(Muted); r.AutoItem().Text(Money(order.Subtotal)); });
                    c.Item().Row(r => { r.RelativeItem().Text("Desconto").FontColor(Muted); r.AutoItem().Text(order.Discount > 0 ? "− " + Money(order.Discount) : Money(0)); });
                    c.Item().PaddingTop(4).BorderTop(1).BorderColor(Primary).PaddingTop(4).Row(r =>
                    {
                        r.RelativeItem().Text("Total").Bold().FontSize(12);
                        r.AutoItem().Text(Money(order.Total)).Bold().FontSize(12).FontColor(Primary);
                    });
                });

                if (!string.IsNullOrWhiteSpace(order.Notes))
                    col.Item().Background(Zebra).Padding(10).Column(c =>
                    {
                        c.Item().Text("OBSERVAÇÕES").FontSize(7.5f).SemiBold().FontColor(Muted);
                        c.Item().Text(order.Notes);
                    });

                if (order.CancelReason is not null)
                    col.Item().Background("#FEE2E2").Padding(10).Text($"Pedido cancelado: {order.CancelReason}").FontColor("#991B1B");
            });

            page.Footer().Element(c => ComposeFooter(c, now));
        })).GeneratePdf();
    }

    private static string? FormatPhone(string? digits) => digits?.Length switch
    {
        11 => $"({digits[..2]}) {digits[2..7]}-{digits[7..]}",
        10 => $"({digits[..2]}) {digits[2..6]}-{digits[6..]}",
        _ => digits,
    };
}
