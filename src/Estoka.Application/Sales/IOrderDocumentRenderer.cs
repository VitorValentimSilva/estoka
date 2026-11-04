namespace Estoka.Application.Sales;

public sealed record CompanyInfo(string Name, string City, string? Document = null, string? Phone = null);

/// <summary>Gera o PDF imprimível do pedido (implementado com QuestPDF na Infrastructure).</summary>
public interface IOrderDocumentRenderer
{
    byte[] RenderPdf(SalesOrderDetails order, CompanyInfo company);
}
