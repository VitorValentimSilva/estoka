using Estoka.Application.Common;
using Estoka.Domain.Common;
using Estoka.Domain.Customers;
using FluentValidation;

namespace Estoka.Application.Customers;

public sealed record CustomerDto(
    Guid Id, PersonType Type, string Name, string Document, string? Email, string? Phone, Address Address, string? Notes,
    int OrderCount)
{
    public string FormattedDocument => DocumentValidator.Format(Document);
}

public sealed record CustomerPick(Guid Id, string Name, string Document)
{
    public override string ToString() => $"{Name} ({DocumentValidator.Format(Document)})";
}

public sealed record CustomerQuery : PageRequest
{
    public string? Search { get; init; }
    public PersonType? Type { get; init; }
}

public sealed class CustomerInput
{
    public PersonType Type { get; set; } = PersonType.PF;
    public string Name { get; set; } = string.Empty;
    public string Document { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? ZipCode { get; set; }
    public string? Street { get; set; }
    public string? Number { get; set; }
    public string? Complement { get; set; }
    public string? District { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Notes { get; set; }

    public Address ToAddress() => new(ZipCode, Street, Number, Complement, District, City, State);

    public static CustomerInput From(CustomerDto c) => new()
    {
        Type = c.Type, Name = c.Name, Document = c.Document, Email = c.Email, Phone = c.Phone,
        ZipCode = c.Address.ZipCode, Street = c.Address.Street, Number = c.Address.Number, Complement = c.Address.Complement,
        District = c.Address.District, City = c.Address.City, State = c.Address.State, Notes = c.Notes,
    };
}

public sealed class CustomerInputValidator : AbstractValidator<CustomerInput>
{
    public CustomerInputValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Name).NotEmpty().WithMessage("Informe o nome.").MaximumLength(150);
        RuleFor(x => x.Document).NotEmpty().WithMessage("Informe o documento.");
        RuleFor(x => x.Document).Must(DocumentValidator.IsValidCpf).WithMessage("CPF inválido.")
            .When(x => x.Type == PersonType.PF && !string.IsNullOrWhiteSpace(x.Document));
        RuleFor(x => x.Document).Must(DocumentValidator.IsValidCnpj).WithMessage("CNPJ inválido.")
            .When(x => x.Type == PersonType.PJ && !string.IsNullOrWhiteSpace(x.Document));
        RuleFor(x => x.Email).EmailAddress().WithMessage("E-mail inválido.").MaximumLength(150)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).ValidPhone();
        RuleFor(x => x.ZipCode).Must(z => string.IsNullOrWhiteSpace(z) || DocumentValidator.OnlyDigits(z).Length == 8)
            .WithMessage("CEP deve ter 8 dígitos.");
        RuleFor(x => x.Street).MaximumLength(150);
        RuleFor(x => x.Number).MaximumLength(20);
        RuleFor(x => x.Complement).MaximumLength(80);
        RuleFor(x => x.District).MaximumLength(80);
        RuleFor(x => x.City).MaximumLength(80);
        RuleFor(x => x.State).ValidState();
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
