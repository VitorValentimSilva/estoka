namespace Estoka.Web.Infrastructure;

/// <summary>Textos configuráveis do produto (appsettings: seção "Branding").</summary>
public sealed class BrandingOptions
{
    public const string Section = "Branding";

    public string CompanyName { get; set; } = "Distribuidora Bom Preço";
    public string CompanyCity { get; set; } = "Campinas – SP";
    public string AuthorName { get; set; } = "Vitor Valentim Silva";
    public string AuthorUrl { get; set; } = "https://github.com/VitorValentimSilva";
    public string RepositoryUrl { get; set; } = "https://github.com/VitorValentimSilva/estoka";
}
