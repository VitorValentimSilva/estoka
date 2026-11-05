using Estoka.Domain.Catalog;

namespace Estoka.Infrastructure.Seed;

/// <summary>Catálogo fictício da "Distribuidora Bom Preço". Marcas inventadas; preços de custo coerentes com atacado.</summary>
internal static class DemoCatalog
{
    internal sealed record CategorySeed(string Prefix, string Name, string Description, int SupplierIndex, ProductSeed[] Products);

    internal sealed record ProductSeed(string Name, UnitOfMeasure Unit, decimal Cost);

    private static ProductSeed P(string name, UnitOfMeasure unit, decimal cost) => new(name, unit, cost);

    private const UnitOfMeasure UN = UnitOfMeasure.Unidade;
    private const UnitOfMeasure CX = UnitOfMeasure.Caixa;
    private const UnitOfMeasure FD = UnitOfMeasure.Fardo;
    private const UnitOfMeasure PCT = UnitOfMeasure.Pacote;
    private const UnitOfMeasure KG = UnitOfMeasure.Quilograma;
    private const UnitOfMeasure GF = UnitOfMeasure.Garrafa;
    private const UnitOfMeasure GL = UnitOfMeasure.Galao;

    public static readonly CategorySeed[] Categories =
    [
        new("BEB", "Bebidas", "Refrigerantes, águas, sucos, cervejas e energéticos", 0,
        [
            P("Refrigerante Cola 2L — fardo c/ 6", FD, 38.90m),
            P("Refrigerante Guaraná 2L — fardo c/ 6", FD, 33.50m),
            P("Refrigerante Laranja 2L — fardo c/ 6", FD, 31.80m),
            P("Refrigerante Cola Lata 350ml — pack c/ 12", PCT, 32.40m),
            P("Água Mineral sem Gás 500ml — fardo c/ 12", FD, 11.90m),
            P("Água Mineral com Gás 500ml — fardo c/ 12", FD, 14.60m),
            P("Água Mineral 1,5L — fardo c/ 6", FD, 12.80m),
            P("Suco de Uva Integral 1L", GF, 9.70m),
            P("Suco Néctar de Pêssego 1L", UN, 4.90m),
            P("Chá Mate Limão 1,5L", GF, 4.30m),
            P("Energético 473ml — pack c/ 6", PCT, 34.90m),
            P("Cerveja Pilsen Lata 350ml — pack c/ 12", PCT, 36.80m),
            P("Cerveja Puro Malte Long Neck 330ml — caixa c/ 24", CX, 98.00m),
            P("Isotônico Limão 500ml — pack c/ 6", PCT, 17.40m),
            P("Água de Coco 1L", UN, 7.20m),
            P("Galão de Água Mineral 20L", GL, 9.50m),
        ]),
        new("MER", "Mercearia", "Grãos, massas, óleos, enlatados e temperos", 1,
        [
            P("Arroz Tipo 1 5kg", PCT, 22.90m),
            P("Feijão Carioca Tipo 1 1kg", PCT, 6.40m),
            P("Feijão Preto 1kg", PCT, 7.10m),
            P("Açúcar Refinado 1kg — fardo c/ 10", FD, 39.50m),
            P("Café Torrado e Moído 500g", PCT, 15.80m),
            P("Óleo de Soja 900ml — caixa c/ 20", CX, 132.00m),
            P("Macarrão Espaguete 500g", PCT, 3.20m),
            P("Macarrão Parafuso 500g", PCT, 3.40m),
            P("Farinha de Trigo 1kg", PCT, 4.30m),
            P("Sal Refinado 1kg", PCT, 1.90m),
            P("Molho de Tomate Tradicional 340g", UN, 1.95m),
            P("Milho Verde em Conserva 170g", UN, 2.60m),
            P("Sardinha em Óleo 125g", UN, 4.70m),
            P("Extrato de Tomate 4,1kg", UN, 27.50m),
            P("Azeite Extravirgem 500ml", GF, 24.90m),
            P("Farofa Temperada 400g", PCT, 3.90m),
        ]),
        new("LIM", "Limpeza", "Produtos de limpeza doméstica e profissional", 2,
        [
            P("Detergente Líquido Neutro 500ml — caixa c/ 24", CX, 42.00m),
            P("Água Sanitária 2L", UN, 4.10m),
            P("Desinfetante Lavanda 2L", UN, 6.80m),
            P("Sabão em Pó 1,6kg", CX, 17.90m),
            P("Sabão em Barra Glicerinado — pacote c/ 5", PCT, 8.40m),
            P("Amaciante Concentrado 2L", UN, 13.70m),
            P("Limpador Multiuso 500ml", UN, 3.60m),
            P("Esponja Dupla Face — pacote c/ 4", PCT, 3.20m),
            P("Saco de Lixo 100L — rolo c/ 50", PCT, 21.50m),
            P("Saco de Lixo 30L — rolo c/ 50", PCT, 9.80m),
            P("Pano de Chão Alvejado", UN, 3.40m),
            P("Álcool Líquido 70% 1L", UN, 7.90m),
            P("Lustra-Móveis 200ml", UN, 5.60m),
            P("Desengordurante Profissional 5L", GL, 38.00m),
            P("Rodo de Alumínio 40cm", UN, 12.50m),
            P("Vassoura de Pelo 30cm", UN, 11.20m),
        ]),
        new("HIG", "Higiene Pessoal", "Higiene, cuidados pessoais e papel higiênico", 3,
        [
            P("Papel Higiênico Folha Dupla — fardo c/ 64 rolos", FD, 79.00m),
            P("Papel Higiênico Folha Simples — pacote c/ 12", PCT, 11.40m),
            P("Sabonete em Barra 85g — caixa c/ 12", CX, 18.60m),
            P("Sabonete Líquido 250ml", UN, 6.90m),
            P("Creme Dental 90g", UN, 3.70m),
            P("Escova Dental Macia", UN, 3.10m),
            P("Shampoo Uso Diário 350ml", UN, 9.80m),
            P("Condicionador Uso Diário 350ml", UN, 10.40m),
            P("Desodorante Aerossol 150ml", UN, 9.90m),
            P("Fralda Descartável G — pacote c/ 30", PCT, 39.90m),
            P("Absorvente com Abas — pacote c/ 8", PCT, 4.20m),
            P("Papel Toalha Interfolhado — fardo c/ 1000", FD, 26.00m),
            P("Lenço Umedecido — pacote c/ 50", PCT, 6.30m),
            P("Algodão em Bolas 50g", PCT, 2.90m),
            P("Enxaguante Bucal 500ml", UN, 12.40m),
        ]),
        new("LAT", "Laticínios e Frios", "Leites, queijos, iogurtes, manteigas e embutidos", 4,
        [
            P("Leite UHT Integral 1L — caixa c/ 12", CX, 52.80m),
            P("Leite UHT Desnatado 1L — caixa c/ 12", CX, 54.00m),
            P("Leite Condensado 395g", UN, 5.40m),
            P("Creme de Leite 200g", UN, 2.90m),
            P("Manteiga com Sal 200g", UN, 10.90m),
            P("Margarina 500g", UN, 6.80m),
            P("Queijo Muçarela Fatiado", KG, 34.90m),
            P("Presunto Cozido Fatiado", KG, 27.50m),
            P("Requeijão Cremoso 200g", UN, 6.20m),
            P("Iogurte Natural 170g", UN, 2.40m),
            P("Iogurte de Morango 1,25kg", UN, 9.90m),
            P("Queijo Parmesão Ralado 50g", UN, 4.60m),
            P("Mortadela Defumada", KG, 14.90m),
            P("Salsicha Tipo Hot Dog", KG, 11.80m),
            P("Queijo Prato Fatiado", KG, 36.90m),
        ]),
        new("MAT", "Padaria e Matinais", "Biscoitos, pães, cereais e achocolatados", 5,
        [
            P("Pão de Forma Tradicional 500g", PCT, 6.40m),
            P("Biscoito Cream Cracker 400g", PCT, 4.60m),
            P("Biscoito Recheado Chocolate 130g", PCT, 1.90m),
            P("Biscoito Maisena 400g", PCT, 4.20m),
            P("Achocolatado em Pó 400g", UN, 6.90m),
            P("Cereal Matinal de Milho 250g", UN, 7.80m),
            P("Aveia em Flocos 500g", UN, 6.10m),
            P("Granola Tradicional 1kg", PCT, 19.90m),
            P("Torrada Tradicional 160g", PCT, 3.80m),
            P("Bolo Pronto de Laranja 250g", UN, 5.30m),
            P("Geleia de Morango 320g", UN, 8.20m),
            P("Mel Silvestre 280g", UN, 14.50m),
            P("Café Solúvel 100g", UN, 10.90m),
            P("Filtro de Café 103 — caixa c/ 30", CX, 4.10m),
            P("Biscoito de Polvilho 100g", PCT, 3.30m),
        ]),
        new("DES", "Descartáveis e Embalagens", "Copos, pratos, guardanapos, embalagens e utensílios", 6,
        [
            P("Copo Descartável 200ml — caixa c/ 2500", CX, 98.00m),
            P("Copo Descartável 50ml — pacote c/ 100", PCT, 3.40m),
            P("Prato Descartável 21cm — pacote c/ 10", PCT, 2.70m),
            P("Guardanapo de Papel 20x20 — pacote c/ 50", PCT, 1.60m),
            P("Talher Descartável Garfo — pacote c/ 50", PCT, 4.20m),
            P("Marmitex Alumínio Nº 8 — caixa c/ 100", CX, 47.00m),
            P("Filme PVC 28cm x 30m", UN, 6.90m),
            P("Papel Alumínio 45cm x 7,5m", UN, 7.40m),
            P("Sacola Plástica 40x50 — pacote 1kg", PCT, 14.80m),
            P("Canudo Biodegradável — pacote c/ 100", PCT, 5.60m),
            P("Pote Descartável 500ml c/ Tampa — pacote c/ 25", PCT, 16.40m),
            P("Luva de Vinil — caixa c/ 100", CX, 22.90m),
            P("Touca Descartável — pacote c/ 100", PCT, 12.30m),
            P("Bobina Picotada 30x40 — rolo", UN, 18.70m),
        ]),
        new("PET", "Pet Shop", "Rações, petiscos e higiene animal", 7,
        [
            P("Ração Cães Adultos 15kg", PCT, 89.90m),
            P("Ração Cães Filhotes 10kg", PCT, 79.00m),
            P("Ração Gatos Adultos 10,1kg", PCT, 94.50m),
            P("Ração Úmida Sachê Gatos 85g", UN, 1.85m),
            P("Petisco Bifinho Carne 65g", PCT, 4.30m),
            P("Areia Higiênica para Gatos 4kg", PCT, 9.40m),
            P("Tapete Higiênico — pacote c/ 30", PCT, 38.00m),
            P("Shampoo Neutro Pet 500ml", UN, 11.90m),
            P("Osso Natural para Cães", UN, 6.80m),
            P("Antipulgas Coleira Média", UN, 24.90m),
            P("Comedouro Inox Médio", UN, 12.60m),
            P("Ração Pássaros Mistura 1kg", PCT, 8.70m),
            P("Brinquedo Corda para Cães", UN, 7.90m),
        ]),
    ];

    /// <summary>Fornecedores fictícios — CNPJs gerados válidos no seed.</summary>
    public static readonly (string CompanyName, string TradeName, string Contact, string City, string State)[] Suppliers =
    [
        ("Paulista Bebidas Distribuição Ltda", "Paulista Bebidas", "Fernanda Rocha", "Campinas", "SP"),
        ("Grãos do Interior Comércio de Alimentos Ltda", "Grãos do Interior", "Marcos Vieira", "Limeira", "SP"),
        ("Brilho Limpo Indústria Química Ltda", "Brilho Limpo", "Patrícia Nunes", "Sumaré", "SP"),
        ("Cuidar Higiene e Cosméticos S.A.", "Cuidar Higiene", "Rafael Teixeira", "São Paulo", "SP"),
        ("Laticínios Serra Azul Ltda", "Serra Azul", "Luciana Prado", "Pouso Alegre", "MG"),
        ("Panificadora e Alimentos Bom Dia Ltda", "Bom Dia Alimentos", "Eduardo Matos", "Jundiaí", "SP"),
        ("Embala Fácil Descartáveis Ltda", "Embala Fácil", "Simone Araújo", "Hortolândia", "SP"),
        ("Amigo Fiel Produtos Pet Ltda", "Amigo Fiel Pet", "Thiago Barros", "Indaiatuba", "SP"),
        ("Atacado Regional Campinas Ltda", "Atacado Regional", "Cláudia Ribeiro", "Campinas", "SP"),
        ("Rota Sul Logística e Comércio Ltda", "Rota Sul", "André Moreira", "Valinhos", "SP"),
    ];

    public static readonly string[] FirstNames =
    [
        "Ana", "Bruno", "Camila", "Daniel", "Eduarda", "Felipe", "Gabriela", "Henrique", "Isabela", "João",
        "Larissa", "Lucas", "Mariana", "Mateus", "Natália", "Otávio", "Paula", "Rafael", "Sabrina", "Thiago",
        "Vanessa", "Vinícius", "Beatriz", "Gustavo", "Letícia", "Rodrigo", "Juliana", "Pedro", "Carolina", "Fábio",
    ];

    public static readonly string[] LastNames =
    [
        "Silva", "Santos", "Oliveira", "Souza", "Rodrigues", "Ferreira", "Alves", "Pereira", "Lima", "Gomes",
        "Costa", "Ribeiro", "Martins", "Carvalho", "Almeida", "Lopes", "Soares", "Fernandes", "Vieira", "Barbosa",
        "Rocha", "Dias", "Nascimento", "Andrade", "Moreira", "Nunes", "Marques", "Machado", "Mendes", "Freitas",
    ];

    public static readonly string[] BusinessNames =
    [
        "Mercadinho São José", "Padaria Pão Dourado", "Restaurante Sabor Caseiro", "Lanchonete Ponto Certo",
        "Empório Cambuí", "Mercearia Dona Rosa", "Bar e Lanches do Zé", "Pizzaria Forno a Lenha",
        "Hotel Recanto das Palmeiras", "Escola Pequeno Saber", "Clínica Bem Viver", "Condomínio Parque das Flores",
        "Supermercado Vila Nova", "Açougue e Mercearia Boi Bom", "Cantina da Nonna", "Academia Corpo em Forma",
        "Pet Shop Patas Felizes", "Buffet Doce Festa", "Lavanderia Bolha de Sabão", "Café Grão de Ouro",
        "Restaurante Tempero Mineiro", "Quitanda do Bairro", "Pousada Serra Verde", "Mini Mercado Avenida",
        "Distribuidora Água Viva",
    ];

    public static readonly (string City, string[] Districts)[] Cities =
    [
        ("Campinas", ["Cambuí", "Barão Geraldo", "Taquaral", "Centro", "Castelo", "Guanabara", "Sousas", "Jardim Proença", "Botafogo", "Ouro Verde"]),
        ("Valinhos", ["Centro", "Jardim Europa", "Vila Santana"]),
        ("Vinhedo", ["Centro", "Capela", "Jardim Itália"]),
        ("Sumaré", ["Centro", "Jardim Nova Veneza", "Matão"]),
        ("Hortolândia", ["Jardim Amanda", "Remanso Campineiro"]),
        ("Paulínia", ["Centro", "Morumbi"]),
        ("Indaiatuba", ["Centro", "Cidade Nova", "Jardim Pau Preto"]),
        ("Americana", ["Centro", "Vila Rehder"]),
    ];

    public static readonly string[] Streets =
    [
        "Rua das Acácias", "Avenida Brasil", "Rua Barão de Jaguara", "Avenida Norte-Sul", "Rua José Paulino",
        "Rua Conceição", "Avenida Orosimbo Maia", "Rua Coronel Quirino", "Rua dos Ipês", "Avenida Independência",
        "Rua Sete de Setembro", "Rua Dom Pedro II", "Rua Tiradentes", "Avenida Princesa d'Oeste", "Rua XV de Novembro",
    ];
}
