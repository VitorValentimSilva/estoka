using Estoka.Domain.Common;
using Estoka.Infrastructure.Seed;

namespace Estoka.UnitTests.Domain;

public class DocumentValidatorTests
{
    [Theory]
    [InlineData("529.982.247-25")]
    [InlineData("52998224725")]
    [InlineData("111.444.777-35")]
    public void Cpf_valido(string cpf) => DocumentValidator.IsValidCpf(cpf).ShouldBeTrue();

    [Theory]
    [InlineData("529.982.247-24")]
    [InlineData("111.111.111-11")]
    [InlineData("123")]
    [InlineData("")]
    [InlineData(null)]
    public void Cpf_invalido(string? cpf) => DocumentValidator.IsValidCpf(cpf).ShouldBeFalse();

    [Theory]
    [InlineData("11.222.333/0001-81")]
    [InlineData("11222333000181")]
    public void Cnpj_valido(string cnpj) => DocumentValidator.IsValidCnpj(cnpj).ShouldBeTrue();

    [Theory]
    [InlineData("11.222.333/0001-80")]
    [InlineData("00.000.000/0000-00")]
    [InlineData("1122233300018")]
    public void Cnpj_invalido(string cnpj) => DocumentValidator.IsValidCnpj(cnpj).ShouldBeFalse();

    [Fact]
    public void Formata_cpf_e_cnpj()
    {
        DocumentValidator.Format("52998224725").ShouldBe("529.982.247-25");
        DocumentValidator.Format("11222333000181").ShouldBe("11.222.333/0001-81");
    }

    [Fact]
    public void Gerador_do_seed_produz_documentos_validos()
    {
        var rnd = new Random(1);
        for (var i = 0; i < 500; i++)
        {
            DocumentValidator.IsValidCpf(DemoDataSeeder.GenerateCpf(rnd)).ShouldBeTrue();
            DocumentValidator.IsValidCnpj(DemoDataSeeder.GenerateCnpj(rnd)).ShouldBeTrue();
        }
    }
}
