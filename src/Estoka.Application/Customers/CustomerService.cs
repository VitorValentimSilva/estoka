using Estoka.Application.Abstractions;
using Estoka.Application.Common;
using Estoka.Application.Security;
using Estoka.Domain.Common;
using Estoka.Domain.Customers;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Estoka.Application.Customers;

public sealed class CustomerService(IAppDbContextFactory dbFactory, ICurrentUser user, IValidator<CustomerInput> validator)
{
    public async Task<PagedResult<CustomerDto>> GetPagedAsync(CustomerQuery query, CancellationToken ct = default)
    {
        user.Demand(Permission.ViewCustomers);
        await using var db = await dbFactory.CreateAsync(ct);
        var q = Filter(db.Customers.AsNoTracking(), query.Search);
        if (query.Type is { } type) q = q.Where(c => c.Type == type);

        var total = await q.CountAsync(ct);
        q = (query.SortBy, query.SortDescending) switch
        {
            ("City", false) => q.OrderBy(c => c.Address.City).ThenBy(c => c.Name),
            ("City", true) => q.OrderByDescending(c => c.Address.City).ThenBy(c => c.Name),
            ("Type", false) => q.OrderBy(c => c.Type).ThenBy(c => c.Name),
            ("Type", true) => q.OrderByDescending(c => c.Type).ThenBy(c => c.Name),
            (_, true) => q.OrderByDescending(c => c.Name),
            _ => q.OrderBy(c => c.Name),
        };
        var items = await q.Page(query)
            .Select(c => new CustomerDto(c.Id, c.Type, c.Name, c.Document, c.Email, c.Phone, c.Address, c.Notes,
                db.SalesOrders.Count(o => o.CustomerId == c.Id)))
            .ToListAsync(ct);
        return new PagedResult<CustomerDto>(items, total);
    }

    public async Task<CustomerDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        user.Demand(Permission.ViewCustomers);
        await using var db = await dbFactory.CreateAsync(ct);
        return await db.Customers.AsNoTracking().Where(c => c.Id == id)
            .Select(c => new CustomerDto(c.Id, c.Type, c.Name, c.Document, c.Email, c.Phone, c.Address, c.Notes,
                db.SalesOrders.Count(o => o.CustomerId == c.Id)))
            .FirstOrDefaultAsync(ct) ?? throw NotFoundException.For("Cliente");
    }

    public async Task<IReadOnlyList<CustomerPick>> SearchAsync(string? term, int take = 15, CancellationToken ct = default)
    {
        user.Demand(Permission.ViewCustomers);
        await using var db = await dbFactory.CreateAsync(ct);
        return await Filter(db.Customers.AsNoTracking(), term).OrderBy(c => c.Name).Take(take)
            .Select(c => new CustomerPick(c.Id, c.Name, c.Document)).ToListAsync(ct);
    }

    private static IQueryable<Customer> Filter(IQueryable<Customer> q, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return q;
        var like = Text.Like(search);
        var digits = DocumentValidator.OnlyDigits(search);
        return q.Where(c => EF.Functions.ILike(c.Name, like) || EF.Functions.ILike(c.Email!, like)
                            || (digits.Length >= 3 && c.Document.Contains(digits)));
    }

    public async Task<Guid> CreateAsync(CustomerInput input, CancellationToken ct = default)
    {
        user.Demand(Permission.ManageCustomers);
        await validator.EnsureValidAsync(input, ct);
        await using var db = await dbFactory.CreateAsync(ct);
        await EnsureUniqueAsync(db, input.Document, null, ct);
        var customer = new Customer(input.Type, input.Name, input.Document, input.Email, input.Phone, input.ToAddress(), input.Notes);
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);
        return customer.Id;
    }

    public async Task UpdateAsync(Guid id, CustomerInput input, CancellationToken ct = default)
    {
        user.Demand(Permission.ManageCustomers);
        await validator.EnsureValidAsync(input, ct);
        await using var db = await dbFactory.CreateAsync(ct);
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw NotFoundException.For("Cliente");
        await EnsureUniqueAsync(db, input.Document, id, ct);
        customer.Update(input.Type, input.Name, input.Document, input.Email, input.Phone, input.ToAddress(), input.Notes);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        user.Demand(Permission.ManageCustomers);
        await using var db = await dbFactory.CreateAsync(ct);
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw NotFoundException.For("Cliente");
        db.Customers.Remove(customer); // soft delete: pedidos antigos continuam referenciando o cliente
        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureUniqueAsync(IAppDbContext db, string document, Guid? id, CancellationToken ct)
    {
        var digits = DocumentValidator.OnlyDigits(document);
        if (await db.Customers.AnyAsync(c => c.Document == digits && c.Id != id, ct))
            throw new DomainException("Já existe um cliente com este documento.");
    }
}
