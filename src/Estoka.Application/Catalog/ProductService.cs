using Estoka.Application.Abstractions;
using Estoka.Application.Common;
using Estoka.Application.Security;
using Estoka.Domain.Catalog;
using Estoka.Domain.Common;
using Estoka.Domain.Inventory;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Estoka.Application.Catalog;

public sealed class ProductService(
    IAppDbContextFactory dbFactory, ICurrentUser user, IValidator<ProductInput> validator, TimeProvider clock)
{
    public async Task<PagedResult<ProductListItem>> GetPagedAsync(ProductQuery query, CancellationToken ct = default)
    {
        user.Demand(Permission.ViewCatalog);
        var canViewCost = user.Can(Permission.ViewCost);
        await using var db = await dbFactory.CreateAsync(ct);

        var q = db.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var like = Text.Like(query.Search);
            var barcode = Gtin.Normalize(query.Search);
            q = q.Where(p => EF.Functions.ILike(p.Name, like) || EF.Functions.ILike(p.Sku, like) || (barcode != null && p.Barcode == barcode));
        }
        if (query.CategoryId is { } categoryId) q = q.Where(p => p.CategoryId == categoryId);
        if (query.BelowMinimumOnly) q = q.Where(p => p.CurrentStock < p.MinimumStock);
        if (query.Active is { } active) q = q.Where(p => p.IsActive == active);

        var total = await q.CountAsync(ct);
        q = ApplySort(q, query.SortBy, query.SortDescending, canViewCost);

        var items = await q.Page(query)
            .Select(p => new ProductListItem(
                p.Id, p.Sku, p.Name, p.Category!.Name, p.Supplier != null ? (p.Supplier.TradeName ?? p.Supplier.CompanyName) : null,
                p.SalePrice,
                canViewCost ? p.CostPrice : null,
                canViewCost ? (p.SalePrice == 0 ? 0 : Math.Round((p.SalePrice - p.CostPrice) / p.SalePrice * 100, 2)) : null,
                p.Unit, p.CurrentStock, p.MinimumStock, p.IsActive, p.Barcode))
            .ToListAsync(ct);
        return new PagedResult<ProductListItem>(items, total);
    }

    private static IQueryable<Product> ApplySort(IQueryable<Product> q, string? sortBy, bool desc, bool canViewCost)
        => (sortBy, desc) switch
        {
            ("Sku", false) => q.OrderBy(p => p.Sku),
            ("Sku", true) => q.OrderByDescending(p => p.Sku),
            ("CategoryName", false) => q.OrderBy(p => p.Category!.Name).ThenBy(p => p.Name),
            ("CategoryName", true) => q.OrderByDescending(p => p.Category!.Name).ThenBy(p => p.Name),
            ("SalePrice", false) => q.OrderBy(p => p.SalePrice),
            ("SalePrice", true) => q.OrderByDescending(p => p.SalePrice),
            ("CostPrice", false) when canViewCost => q.OrderBy(p => p.CostPrice),
            ("CostPrice", true) when canViewCost => q.OrderByDescending(p => p.CostPrice),
            ("MarginPercent", false) when canViewCost => q.OrderBy(p => (p.SalePrice - p.CostPrice) / p.SalePrice),
            ("MarginPercent", true) when canViewCost => q.OrderByDescending(p => (p.SalePrice - p.CostPrice) / p.SalePrice),
            ("CurrentStock", false) => q.OrderBy(p => p.CurrentStock),
            ("CurrentStock", true) => q.OrderByDescending(p => p.CurrentStock),
            ("Name", true) => q.OrderByDescending(p => p.Name),
            _ => q.OrderBy(p => p.Name),
        };

    public async Task<ProductDetails> GetAsync(Guid id, CancellationToken ct = default)
    {
        user.Demand(Permission.ViewCatalog);
        var canViewCost = user.Can(Permission.ViewCost);
        await using var db = await dbFactory.CreateAsync(ct);
        var p = await db.Products.AsNoTracking().Include(x => x.Category).Include(x => x.Supplier)
            .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Produto");
        return new ProductDetails(p.Id, p.Sku, p.Name, p.Description, p.CategoryId, p.Category!.Name,
            p.SupplierId, p.Supplier?.DisplayName, p.SalePrice,
            canViewCost ? p.CostPrice : null, canViewCost ? p.MarginPercent : null,
            p.Unit, p.CurrentStock, p.MinimumStock, p.IsActive, p.ImageUrl, p.CreatedAt, p.UpdatedAt, p.UpdatedBy, p.Barcode);
    }

    /// <summary>Custo e margem — exclusivo de quem tem <c>ViewCost</c>. Lança <see cref="ForbiddenException"/> para Vendedor.</summary>
    public async Task<(decimal CostPrice, decimal MarginPercent)> GetCostAsync(Guid id, CancellationToken ct = default)
    {
        user.Demand(Permission.ViewCost);
        await using var db = await dbFactory.CreateAsync(ct);
        var p = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFoundException.For("Produto");
        return (p.CostPrice, p.MarginPercent);
    }

    public Task<IReadOnlyList<ProductPick>> SearchForSaleAsync(string? term, int take = 15, CancellationToken ct = default)
        => SearchAsync(term, take, includeInactive: false, ct);

    /// <summary>Busca para movimentação de estoque: inclui inativos (ex.: repor um produto antes de reativá-lo).</summary>
    public Task<IReadOnlyList<ProductPick>> SearchForStockAsync(string? term, int take = 15, CancellationToken ct = default)
        => SearchAsync(term, take, includeInactive: true, ct);

    /// <summary>
    /// Localiza um produto pelo código de barras lido (leitor USB, câmera ou digitado). Retorna null se não existir.
    /// Código com dígito verificador inválido gera <see cref="DomainException"/> (leitura com erro).
    /// </summary>
    public async Task<ProductPick?> FindByBarcodeAsync(string? code, bool includeInactive = false, CancellationToken ct = default)
    {
        user.Demand(Permission.ViewCatalog);
        var barcode = Gtin.Normalize(code);
        if (barcode is null) return null;
        if (!Gtin.IsValid(barcode)) throw new DomainException($"Código de barras \"{barcode}\" inválido — verifique a leitura.");
        await using var db = await dbFactory.CreateAsync(ct);
        return await db.Products.AsNoTracking()
            .Where(p => p.Barcode == barcode && (includeInactive || p.IsActive))
            .Select(p => new ProductPick(p.Id, p.Sku, p.Name, p.SalePrice, p.CurrentStock, p.Unit, p.IsActive))
            .FirstOrDefaultAsync(ct);
    }

    private async Task<IReadOnlyList<ProductPick>> SearchAsync(string? term, int take, bool includeInactive, CancellationToken ct)
    {
        user.Demand(Permission.ViewCatalog);
        await using var db = await dbFactory.CreateAsync(ct);
        var q = db.Products.AsNoTracking().Where(p => includeInactive || p.IsActive);
        if (!string.IsNullOrWhiteSpace(term))
        {
            var like = Text.Like(term);
            var barcode = Gtin.Normalize(term);
            q = q.Where(p => EF.Functions.ILike(p.Name, like) || EF.Functions.ILike(p.Sku, like) || (barcode != null && p.Barcode == barcode));
        }
        return await q.OrderBy(p => p.Name).Take(take)
            .Select(p => new ProductPick(p.Id, p.Sku, p.Name, p.SalePrice, p.CurrentStock, p.Unit, p.IsActive))
            .ToListAsync(ct);
    }

    public async Task<Guid> CreateAsync(ProductInput input, CancellationToken ct = default)
    {
        user.Demand(Permission.ManageCatalog);
        await validator.EnsureValidAsync(input, ct);
        await using var db = await dbFactory.CreateAsync(ct);
        await EnsureReferencesAsync(db, input, null, ct);

        var product = new Product(input.Sku, input.Name, input.Description, input.CategoryId, input.SupplierId,
            input.CostPrice, input.SalePrice, input.Unit, input.MinimumStock, input.ImageUrl);
        if (!input.IsActive) product.Deactivate();
        product.SetBarcode(input.Barcode);
        db.Products.Add(product);

        if (input.InitialStock > 0)
            db.StockMovements.Add(product.RegisterMovement(StockMovementType.Entrada, input.InitialStock,
                "Estoque inicial", user.UserName, clock.GetUtcNow(), input.CostPrice));

        await db.SaveChangesAsync(ct);
        return product.Id;
    }

    public async Task UpdateAsync(Guid id, ProductInput input, CancellationToken ct = default)
    {
        user.Demand(Permission.ManageCatalog);
        await validator.EnsureValidAsync(input, ct);
        await using var db = await dbFactory.CreateAsync(ct);
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw NotFoundException.For("Produto");
        await EnsureReferencesAsync(db, input, id, ct);

        // Quem não vê custo também não pode alterá-lo.
        var cost = user.Can(Permission.ViewCost) ? input.CostPrice : product.CostPrice;
        product.Update(input.Sku, input.Name, input.Description, input.CategoryId, input.SupplierId,
            cost, input.SalePrice, input.Unit, input.MinimumStock, input.IsActive, input.ImageUrl);
        product.SetBarcode(input.Barcode);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        user.Demand(Permission.ManageCatalog);
        await using var db = await dbFactory.CreateAsync(ct);
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw NotFoundException.For("Produto");
        db.Products.Remove(product); // soft delete via interceptor
        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureReferencesAsync(IAppDbContext db, ProductInput input, Guid? id, CancellationToken ct)
    {
        var sku = input.Sku.Trim().ToUpperInvariant();
        if (await db.Products.AnyAsync(p => p.Sku == sku && p.Id != id, ct))
            throw new DomainException($"Já existe um produto com o SKU \"{sku}\".");
        if (Gtin.Normalize(input.Barcode) is { } barcode
            && await db.Products.Where(p => p.Barcode == barcode && p.Id != id).Select(p => p.Name).FirstOrDefaultAsync(ct) is { } other)
            throw new DomainException($"O código de barras {barcode} já pertence a \"{other}\".");
        if (!await db.Categories.AnyAsync(c => c.Id == input.CategoryId, ct))
            throw new DomainException("Categoria não encontrada.");
        if (input.SupplierId is { } supplierId && supplierId != Guid.Empty && !await db.Suppliers.AnyAsync(s => s.Id == supplierId, ct))
            throw new DomainException("Fornecedor não encontrado.");
    }
}
