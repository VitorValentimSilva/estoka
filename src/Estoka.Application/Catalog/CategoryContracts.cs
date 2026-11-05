using FluentValidation;

namespace Estoka.Application.Catalog;

public sealed record CategoryDto(Guid Id, string Name, string? Description, int ProductCount);

public sealed record LookupItem(Guid Id, string Name);

public sealed class CategoryInput
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public sealed class CategoryInputValidator : AbstractValidator<CategoryInput>
{
    public CategoryInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Informe o nome.").MaximumLength(80);
        RuleFor(x => x.Description).MaximumLength(300);
    }
}
