using Estoka.Domain.Common;

namespace Estoka.Domain.Customers;

public enum PersonType
{
    /// <summary>Pessoa física (CPF).</summary>
    PF = 1,
    /// <summary>Pessoa jurídica (CNPJ).</summary>
    PJ = 2,
}

public sealed record Address(
    string? ZipCode,
    string? Street,
    string? Number,
    string? Complement,
    string? District,
    string? City,
    string? State)
{
    public static Address Empty => new(null, null, null, null, null, null, null);

    public override string ToString()
    {
        var line1 = string.Join(", ", new[] { Street, Number, Complement }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var line2 = string.Join(" - ", new[] { District, City, State }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return string.Join(" — ", new[] { line1, line2 }.Where(s => s.Length > 0));
    }
}

public sealed class Customer : SoftDeletableEntity
{
    private Customer() { Name = string.Empty; Document = string.Empty; Address = Address.Empty; }

    public Customer(PersonType type, string name, string document, string? email, string? phone, Address address, string? notes = null)
    {
        Name = string.Empty;
        Document = string.Empty;
        Address = Address.Empty;
        Update(type, name, document, email, phone, address, notes);
    }

    public PersonType Type { get; private set; }
    public string Name { get; private set; }
    /// <summary>CPF ou CNPJ somente com dígitos.</summary>
    public string Document { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public Address Address { get; private set; }
    public string? Notes { get; private set; }

    public string FormattedDocument => DocumentValidator.Format(Document);

    public void Update(PersonType type, string name, string document, string? email, string? phone, Address address, string? notes)
    {
        var valid = type switch
        {
            PersonType.PF => DocumentValidator.IsValidCpf(document),
            PersonType.PJ => DocumentValidator.IsValidCnpj(document),
            _ => throw new DomainException("Tipo de pessoa inválido."),
        };
        if (!valid) throw new DomainException(type == PersonType.PF ? "CPF inválido." : "CNPJ inválido.");

        Type = type;
        Name = Guard.NotEmpty(name, "Nome", 150);
        Document = DocumentValidator.OnlyDigits(document);
        Email = Guard.Optional(email, "E-mail", 150)?.ToLowerInvariant();
        Phone = Guard.Optional(DocumentValidator.OnlyDigits(phone), "Telefone", 11);
        Address = address with
        {
            ZipCode = Guard.Optional(DocumentValidator.OnlyDigits(address.ZipCode), "CEP", 8),
            State = Guard.Optional(address.State, "UF", 2)?.ToUpperInvariant(),
        };
        Notes = Guard.Optional(notes, "Observações", 500);
    }
}
