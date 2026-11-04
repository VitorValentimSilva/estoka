using Estoka.Domain.Common;

namespace Estoka.Domain.Catalog;

public sealed class Category : SoftDeletableEntity
{
    public const int NameMaxLength = 80;
    public const int DescriptionMaxLength = 300;

    private Category() { Name = string.Empty; }

    public Category(string name, string? description = null)
    {
        Name = string.Empty;
        Update(name, description);
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }

    public void Update(string name, string? description)
    {
        Name = Guard.NotEmpty(name, "Nome", NameMaxLength);
        Description = Guard.Optional(description, "Descrição", DescriptionMaxLength);
    }
}
