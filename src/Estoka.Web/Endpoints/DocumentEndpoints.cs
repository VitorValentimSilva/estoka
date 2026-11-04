using Estoka.Application.Reports;
using Estoka.Application.Sales;
using Estoka.Web.Infrastructure;
using Microsoft.Extensions.Options;

namespace Estoka.Web.Endpoints;

/// <summary>Downloads de documentos (PDF/Excel) para usuários logados via cookie.</summary>
internal static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/pedidos/{id:guid}/pdf", async (Guid id, bool? download, SalesOrderService orders, IOrderDocumentRenderer renderer,
                IOptions<BrandingOptions> branding, CancellationToken ct) =>
            {
                var order = await orders.GetAsync(id, ct);
                var pdf = renderer.RenderPdf(order, branding.Value.ToCompanyInfo());
                return download == true
                    ? Results.File(pdf, "application/pdf", $"{order.Code}.pdf")
                    : Results.File(pdf, "application/pdf");
            })
            .RequireAuthorization(Policies.ViewOrders);

        // /relatorios/exportar/vendas.xlsx?de=2026-10-01&ate=2026-10-31 (formatos: xlsx | pdf)
        app.MapGet("/relatorios/exportar/{kind}.{format}", async (string kind, string format, DateOnly? de, DateOnly? ate,
                ReportService reports, IReportExporter exporter, IOptions<BrandingOptions> branding, CancellationToken ct) =>
            {
                if (!Enum.TryParse<ReportKind>(kind.Replace("-", ""), ignoreCase: true, out var reportKind)
                    || format is not ("xlsx" or "pdf"))
                    return Results.NotFound();

                var period = de is { } from && ate is { } to ? new ReportPeriod(from, to) : reports.DefaultPeriod();
                var company = branding.Value.ToCompanyInfo();
                var pdf = format == "pdf";
                var (bytes, name) = reportKind switch
                {
                    ReportKind.Vendas => await Render(await reports.SalesAsync(period, ct), exporter.ToExcel, r => exporter.ToPdf(r, company), $"vendas_{period.From:yyyyMMdd}-{period.To:yyyyMMdd}"),
                    ReportKind.CurvaAbc => await Render(await reports.AbcAsync(period, ct), exporter.ToExcel, r => exporter.ToPdf(r, company), $"curva-abc_{period.From:yyyyMMdd}-{period.To:yyyyMMdd}"),
                    _ => await Render(await reports.StockPositionAsync(ct), exporter.ToExcel, r => exporter.ToPdf(r, company), $"posicao-estoque_{DateTime.Now:yyyyMMdd}"),
                };
                return Results.File(bytes, pdf ? "application/pdf" : XlsxContentType, $"{name}.{format}");

                Task<(byte[], string)> Render<T>(T report, Func<T, byte[]> excel, Func<T, byte[]> toPdf, string fileName)
                    => Task.FromResult((pdf ? toPdf(report) : excel(report), fileName));
            })
            .RequireAuthorization(Policies.ViewReports);
    }

    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static CompanyInfo ToCompanyInfo(this BrandingOptions b) => new(b.CompanyName, b.CompanyCity);
}
