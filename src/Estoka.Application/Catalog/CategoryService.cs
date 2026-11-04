using Estoka.Application.Abstractions;
using Estoka.Application.Common;
using Estoka.Application.Security;
using Estoka.Domain.Catalog;
using Estoka.Domain.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Estoka.Application.Catalog;

public sealed class CategoryService(IAppDbContextFactory dbFactory, ICurrentUser user, IValidator<CategoryInput> validator)
{
    public async Task<IReadOnlyList<CategoryDto>> ListAsync(string? search = null, CancellationToken ct = default)
    {
        user.Demand(Permission.ViewCatalog);
        await using var db = await dbFactory.CreateAsync(ct);
        var query = db.Categories.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => EF.Functions.ILike(c.Name, Text.Like(search)));
        return await query
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description, db.Products.Count(p => p.CategoryId == c.Id)))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LookupItem>> LookupAsync(CancellationToken ct = default)
    {
        user.Demand(Permission.ViewCatalog);
        await using var db = await dbFactory.CreateAsync(ct);
        return await db.Categories.AsNoTracking().OrderBy(c => c.Name).Select(c => new LookupItem(c.Id, c.Name)).ToListAsync(ct);
    }

    public async Task<Guid> CreateAsync(CategoryInput input, CancellationToken ct = default)
    {
        user.Demand(Permission.ManageCatalog);
        await validator.EnsureValidAsync(input, ct);
        await using var db = await dbFactory.CreateAsync(ct);
        await EnsureUniqueAsync(db, input.Name, null, ct);
        var category = new Category(input.Name, input.Description);
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        return category.Id;
    }

    public async Task UpdateAsync(Guid id, CategoryInput input, CancellationToken ct = default)
    {
        user.Demand(Permission.ManageCatalog);
        await validator.EnsureValidAsync(input, ct);
        await using var db = await dbFactory.CreateAsync(ct);
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw NotFoundException.For("Categoria");
        await EnsureUniqueAsync(db, input.Name, id, ct);
        category.Update(input.Name, input.Description);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        user.Demand(Permission.ManageCatalog);
        await using var db = await dbFactory.CreateAsync(ct);
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw NotFoundException.For("Categoria");
        if (await db.Products.AnyAsync(p => p.CategoryId == id, ct))
            throw new DomainException("Não é possível excluir: existem produtos nesta categoria.");
        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureUniqueAsync(IAppDbContext db, string name, Guid? id, CancellationToken ct)
    {
        var normalized = name.Trim().ToLower();
        if (await db.Categories.AnyAsync(c => c.Name.ToLower() == normalized && c.Id != id, ct))
            throw new DomainException($"Já existe uma categoria chamada \"{name.Trim()}\".");
    }
}
