using Estoka.Domain.Common;
using Estoka.Infrastructure.Seed;

namespace Estoka.UnitTests.Domain;

public class GtinTests
{
    [Theory]
    [InlineData("7891000315507")]   // EAN-13
    [InlineData("789-1000-315507")] // com separadores (leitores/etiquetas)
    [InlineData("96385074")]        // EAN-8
    [InlineData("036000291452")]    // UPC-A
    [InlineData("10012345678902")]  // GTIN-14
    public void Codigos_validos(string code) => Gtin.IsValid(code).ShouldBeTrue();

    [Theory]
    [InlineData("7891000315508")] // dígito verificador errado
    [InlineData("789100031550")]  // tamanho inválido (12 sem ser UPC válido)
    [InlineData("12345")]
    [InlineData("ABC1000315507")]
    [InlineData("")]
    [InlineData(null)]
    public void Codigos_invalidos(string? code) => Gtin.IsValid(code).ShouldBeFalse();

    [Fact]
    public void Codigos_da_demo_sao_validos_unicos_e_da_faixa_de_uso_interno()
    {
        var codes = Enumerable.Range(1, 8).SelectMany(c => Enumerable.Range(1, 20).Select(n => DemoDataSeeder.DemoBarcode(c, n))).ToList();
        codes.ShouldAllBe(c => Gtin.IsValid(c) && c.Length == 13 && c.StartsWith("20"));
        codes.Distinct().Count().ShouldBe(codes.Count);
    }

    [Fact]
    public void Produto_rejeita_codigo_invalido_e_normaliza_o_valido()
    {
        var p = TestData.Product();
        Should.Throw<DomainException>(() => p.SetBarcode("7891000315508"));
        p.SetBarcode(" 789-1000-315507 ");
        p.Barcode.ShouldBe("7891000315507");
        p.SetBarcode("");
        p.Barcode.ShouldBeNull();
    }
}
