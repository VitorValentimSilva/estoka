namespace Estoka.Domain.Catalog;

public enum UnitOfMeasure
{
    Unidade = 1,
    Caixa = 2,
    Fardo = 3,
    Pacote = 4,
    Quilograma = 5,
    Litro = 6,
    Garrafa = 7,
    Galao = 8,
}

public static class UnitOfMeasureExtensions
{
    public static string Abbreviation(this UnitOfMeasure unit) => unit switch
    {
        UnitOfMeasure.Unidade => "UN",
        UnitOfMeasure.Caixa => "CX",
        UnitOfMeasure.Fardo => "FD",
        UnitOfMeasure.Pacote => "PCT",
        UnitOfMeasure.Quilograma => "KG",
        UnitOfMeasure.Litro => "L",
        UnitOfMeasure.Garrafa => "GF",
        UnitOfMeasure.Galao => "GL",
        _ => unit.ToString(),
    };
}
