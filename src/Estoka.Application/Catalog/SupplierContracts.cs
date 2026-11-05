using Estoka.Application.Common;
using FluentValidation;

namespace Estoka.Application.Catalog;

public sealed record SupplierDto(
    Guid Id, string CompanyName, string? TradeName, string Cnpj, string? Email, string? Phone,
    string? ContactName, string? City, string? State, string? Notes, int ProductCount)
{
    public string DisplayName => TradeName ?? CompanyName;
}

public sealed record SupplierQuery : PageRequest
{
    public string? Search { get; init; }
}

public sealed class SupplierInput
{
    public string CompanyName { get; set; } = string.Empty;
    public string? TradeName { get; set; }
    public string Cnpj { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? ContactName { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Notes { get; set; }

    public static SupplierInput From(SupplierDto dto) => new()
    {
        CompanyName = dto.CompanyName, TradeName = dto.TradeName, Cnpj = dto.Cnpj, Email = dto.Email, Phone = dto.Phone,
        ContactName = dto.ContactName, City = dto.City, State = dto.State, Notes = dto.Notes,
    };
}

public sealed class SupplierInputValidator : AbstractValidator<SupplierInput>
{
    public SupplierInputValidator()
    {
        RuleFor(x => x.CompanyName).NotEmpty().WithMessage("Informe a razão social.").MaximumLength(150);
        RuleFor(x => x.TradeName).MaximumLength(150);
        RuleFor(x => x.Cnpj).NotEmpty().WithMessage("Informe o CNPJ.").ValidCnpj();
        RuleFor(x => x.Email).EmailAddress().WithMessage("E-mail inválido.").MaximumLength(150)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).ValidPhone();
        RuleFor(x => x.ContactName).MaximumLength(100);
        RuleFor(x => x.City).MaximumLength(80);
        RuleFor(x => x.State).ValidState();
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
