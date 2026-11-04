using Estoka.Domain.Common;

namespace Estoka.Domain.Catalog;

public sealed class Supplier : SoftDeletableEntity
{
    private Supplier() { CompanyName = string.Empty; Cnpj = string.Empty; }

    public Supplier(string companyName, string? tradeName, string cnpj, string? email, string? phone,
        string? contactName, string? city, string? state, string? notes = null)
    {
        CompanyName = string.Empty;
        Cnpj = string.Empty;
        Update(companyName, tradeName, cnpj, email, phone, contactName, city, state, notes);
    }

    /// <summary>Razão social.</summary>
    public string CompanyName { get; private set; }
    /// <summary>Nome fantasia.</summary>
    public string? TradeName { get; private set; }
    /// <summary>CNPJ somente com dígitos.</summary>
    public string Cnpj { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? ContactName { get; private set; }
    public string? City { get; private set; }
    public string? State { get; private set; }
    public string? Notes { get; private set; }

    public string DisplayName => TradeName ?? CompanyName;

    public void Update(string companyName, string? tradeName, string cnpj, string? email, string? phone,
        string? contactName, string? city, string? state, string? notes)
    {
        if (!DocumentValidator.IsValidCnpj(cnpj))
            throw new DomainException("CNPJ inválido.");
        CompanyName = Guard.NotEmpty(companyName, "Razão social", 150);
        TradeName = Guard.Optional(tradeName, "Nome fantasia", 150);
        Cnpj = DocumentValidator.OnlyDigits(cnpj);
        Email = Guard.Optional(email, "E-mail", 150)?.ToLowerInvariant();
        Phone = Guard.Optional(DocumentValidator.OnlyDigits(phone), "Telefone", 11);
        ContactName = Guard.Optional(contactName, "Contato", 100);
        City = Guard.Optional(city, "Cidade", 80);
        State = Guard.Optional(state, "UF", 2)?.ToUpperInvariant();
        Notes = Guard.Optional(notes, "Observações", 500);
    }
}
