using Estoka.Application.Abstractions;
using Estoka.Application.Common;
using Estoka.Application.Security;
using Estoka.Domain.Catalog;
using Estoka.Domain.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Estoka.Application.Catalog;

public sealed class SupplierService(IAppDbContextFactory dbFactory, ICurrentUser user, IValidator<SupplierInput> validator)
{
    public async Task<PagedResult<SupplierDto>> GetPagedAsync(SupplierQuery query, CancellationToken ct = default)
    {
        user.Demand(Permission.ViewCatalog);
        await using var db = await dbFactory.CreateAsync(ct);
        var q = db.Suppliers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var like = Text.Like(query.Search);
            var digits = DocumentValidator.OnlyDigits(query.Search);
            q = q.Where(s => EF.Functions.ILike(s.CompanyName, like) || EF.Functions.ILike(s.TradeName!, like)
                             || (digits.Length > 0 && s.Cnpj.Contains(digits)));
        }

        var total = await q.CountAsync(ct);
        q = (query.SortBy, query.SortDescending) switch
        {
            ("City", false) => q.OrderBy(s => s.City),
            ("City", true) => q.OrderByDescending(s => s.City),
            (_, true) => q.OrderByDescending(s => s.TradeName ?? s.CompanyName),
            _ => q.OrderBy(s => s.TradeName ?? s.CompanyName),
        };
        var items = await q.Page(query)
            .Select(s => new SupplierDto(s.Id, s.CompanyName, s.TradeName, s.Cnpj, s.Email, s.Phone, s.ContactName,
                s.City, s.State, s.Notes, db.Products.Count(p => p.SupplierId == s.Id)))
            .ToListAsync(ct);
        return new PagedResult<SupplierDto>(items, total);
    }

    public async Task<IReadOnlyList<LookupItem>> LookupAsync(CancellationToken ct = default)
    {
        user.Demand(Permission.ViewCatalog);
        await using var db = await dbFactory.CreateAsync(ct);
        return await db.Suppliers.AsNoTracking()
            .OrderBy(s => s.TradeName ?? s.CompanyName)
            .Select(s => new LookupItem(s.Id, s.TradeName ?? s.CompanyName))
            .ToListAsync(ct);
    }

    public async Task<Guid> CreateAsync(SupplierInput input, CancellationToken ct = default)
    {
        user.Demand(Permission.ManageCatalog);
        await validator.EnsureValidAsync(input, ct);
        await using var db = await dbFactory.CreateAsync(ct);
        await EnsureUniqueAsync(db, input.Cnpj, null, ct);
        var supplier = new Supplier(input.CompanyName, input.TradeName, input.Cnpj, input.Email, input.Phone,
            input.ContactName, input.City, input.State, input.Notes);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(ct);
        return supplier.Id;
    }

    public async Task UpdateAsync(Guid id, SupplierInput input, CancellationToken ct = default)
    {
        user.Demand(Permission.ManageCatalog);
        await validator.EnsureValidAsync(input, ct);
        await using var db = await dbFactory.CreateAsync(ct);
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw NotFoundException.For("Fornecedor");
        await EnsureUniqueAsync(db, input.Cnpj, id, ct);
        supplier.Update(input.CompanyName, input.TradeName, input.Cnpj, input.Email, input.Phone,
            input.ContactName, input.City, input.State, input.Notes);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        user.Demand(Permission.ManageCatalog);
        await using var db = await dbFactory.CreateAsync(ct);
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw NotFoundException.For("Fornecedor");
        if (await db.Products.AnyAsync(p => p.SupplierId == id, ct))
            throw new DomainException("Não é possível excluir: existem produtos vinculados a este fornecedor.");
        db.Suppliers.Remove(supplier);
        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureUniqueAsync(IAppDbContext db, string cnpj, Guid? id, CancellationToken ct)
    {
        var digits = DocumentValidator.OnlyDigits(cnpj);
        if (await db.Suppliers.AnyAsync(s => s.Cnpj == digits && s.Id != id, ct))
            throw new DomainException("Já existe um fornecedor com este CNPJ.");
    }
}
