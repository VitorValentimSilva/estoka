using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Estoka.Infrastructure.Exports;

internal static class PdfStyles
{
    public const string Primary = "#0F766E";
    public const string Muted = "#5B6B79";
    public const string Line = "#E3E8EE";
    public const string Zebra = "#F6F8FA";

    public static readonly System.Globalization.CultureInfo PtBr = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");

    public static string Money(decimal value) => value.ToString("C2", PtBr);
    public static string Qty(decimal value) => value.ToString(value % 1 == 0 ? "N0" : "N2", PtBr);

    public static IContainer HeaderCell(IContainer c) => c.BorderBottom(1).BorderColor(Primary).PaddingVertical(5).PaddingHorizontal(4)
        .DefaultTextStyle(x => x.SemiBold().FontSize(8.5f).FontColor(Muted));

    public static IContainer BodyCell(IContainer c, bool zebra) => c.Background(zebra ? Zebra : Colors.White)
        .BorderBottom(0.5f).BorderColor(Line).PaddingVertical(4).PaddingHorizontal(4);

    /// <summary>Cabeçalho padrão: nome da empresa + título do documento.</summary>
    public static void ComposeHeader(IContainer container, string companyName, string companyCity, string title, string? subtitle)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(companyName).FontSize(15).Bold().FontColor(Primary);
                col.Item().Text(companyCity).FontSize(9).FontColor(Muted);
            });
            row.RelativeItem().AlignRight().Column(col =>
            {
                col.Item().AlignRight().Text(title).FontSize(14).Bold();
                if (subtitle is not null) col.Item().AlignRight().Text(subtitle).FontSize(9).FontColor(Muted);
            });
        });
    }

    public static void ComposeFooter(IContainer container, DateTimeOffset generatedAt)
    {
        container.BorderTop(0.5f).BorderColor(Line).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text($"Gerado pelo Estoka em {generatedAt:dd/MM/yyyy HH:mm} · documento sem valor fiscal")
                .FontSize(7.5f).FontColor(Muted);
            row.ConstantItem(80).AlignRight().Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Muted));
                t.Span("Página ");
                t.CurrentPageNumber();
                t.Span(" de ");
                t.TotalPages();
            });
        });
    }
}
