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
    }

    public static CompanyInfo ToCompanyInfo(this BrandingOptions b) => new(b.CompanyName, b.CompanyCity);
}
