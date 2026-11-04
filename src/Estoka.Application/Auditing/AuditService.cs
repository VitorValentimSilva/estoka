using System.Text.Json;
using Estoka.Application.Abstractions;
using Estoka.Application.Common;
using Estoka.Application.Security;
using Estoka.Domain.Auditing;
using Microsoft.EntityFrameworkCore;

namespace Estoka.Application.Auditing;

public sealed record FieldChange(string Field, string? OldValue, string? NewValue);

public sealed record AuditEntryDto(long Id, DateTimeOffset Timestamp, string EntityName, string EntityLabel, string EntityId,
    string? Description, AuditAction Action, string? UserName, IReadOnlyList<FieldChange> Changes);

public sealed record AuditQuery : PageRequest
{
    public string? EntityName { get; init; }
    public AuditAction? Action { get; init; }
    public string? UserName { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

public sealed class AuditService(IAppDbContextFactory dbFactory, ICurrentUser user)
{
    public static readonly IReadOnlyDictionary<string, string> EntityLabels = new Dictionary<string, string>
    {
        ["Product"] = "Produto", ["Category"] = "Categoria", ["Supplier"] = "Fornecedor", ["Customer"] = "Cliente",
        ["SalesOrder"] = "Pedido", ["Usuario"] = "Usuário",
    };

    private static readonly Dictionary<string, string> FieldLabels = new()
    {
        ["Name"] = "Nome", ["Sku"] = "SKU", ["Description"] = "Descrição", ["CategoryId"] = "Categoria", ["SupplierId"] = "Fornecedor",
        ["CostPrice"] = "Preço de custo", ["SalePrice"] = "Preço de venda", ["Unit"] = "Unidade", ["CurrentStock"] = "Estoque atual",
        ["MinimumStock"] = "Estoque mínimo", ["IsActive"] = "Ativo", ["ImageUrl"] = "Imagem", ["IsDeleted"] = "Excluído",
        ["CompanyName"] = "Razão social", ["TradeName"] = "Nome fantasia", ["Cnpj"] = "CNPJ", ["Email"] = "E-mail", ["Phone"] = "Telefone",
        ["ContactName"] = "Contato", ["City"] = "Cidade", ["State"] = "UF", ["Notes"] = "Observações", ["Type"] = "Tipo",
        ["Document"] = "Documento", ["Status"] = "Status", ["Discount"] = "Desconto", ["Subtotal"] = "Subtotal", ["Total"] = "Total",
        ["PaymentMethod"] = "Pagamento", ["CustomerId"] = "Cliente", ["CancelReason"] = "Motivo do cancelamento",
        ["ConfirmedAt"] = "Confirmado em", ["InvoicedAt"] = "Faturado em", ["DeliveredAt"] = "Entregue em", ["CanceledAt"] = "Cancelado em",
        ["OrderDate"] = "Data", ["SellerName"] = "Vendedor", ["Number"] = "Número", ["Role"] = "Papel", ["FullName"] = "Nome",
        ["Address.Street"] = "Logradouro", ["Address.Number"] = "Número", ["Address.City"] = "Cidade", ["Address.State"] = "UF",
        ["Address.District"] = "Bairro", ["Address.ZipCode"] = "CEP", ["Address.Complement"] = "Complemento",
    };

    public async Task<PagedResult<AuditEntryDto>> GetPagedAsync(AuditQuery query, CancellationToken ct = default)
    {
        user.Demand(Permission.ViewAudit);
        await using var db = await dbFactory.CreateAsync(ct);
        var q = db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.EntityName)) q = q.Where(a => a.EntityName == query.EntityName);
        if (query.Action is { } action) q = q.Where(a => a.Action == action);
        if (!string.IsNullOrWhiteSpace(query.UserName)) q = q.Where(a => EF.Functions.ILike(a.UserName!, Text.Like(query.UserName)));
        if (query.From is { } from)
        {
            var start = BrazilTime.StartOfDayUtc(from);
            q = q.Where(a => a.Timestamp >= start);
        }
        if (query.To is { } to)
        {
            var end = BrazilTime.StartOfDayUtc(to.AddDays(1));
            q = q.Where(a => a.Timestamp < end);
        }

        var total = await q.CountAsync(ct);
        var logs = await q.OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id).Page(query).ToListAsync(ct);
        var names = await ResolveNamesAsync(db, logs, ct);

        var items = logs.Select(a => new AuditEntryDto(a.Id, a.Timestamp, a.EntityName, EntityLabels.GetValueOrDefault(a.EntityName, a.EntityName),
                a.EntityId, names.GetValueOrDefault((a.EntityName, a.EntityId)), a.Action, a.UserName, Diff(a)))
            .ToList();
        return new PagedResult<AuditEntryDto>(items, total);
    }

    public async Task<IReadOnlyList<string>> UsersAsync(CancellationToken ct = default)
    {
        user.Demand(Permission.ViewAudit);
        await using var db = await dbFactory.CreateAsync(ct);
        return await db.AuditLogs.Where(a => a.UserName != null).Select(a => a.UserName!).Distinct().OrderBy(n => n).ToListAsync(ct);
    }

    private static async Task<Dictionary<(string, string), string>> ResolveNamesAsync(IAppDbContext db, List<AuditLog> logs, CancellationToken ct)
    {
        var result = new Dictionary<(string, string), string>();
        List<Guid> Ids(string entity) => logs.Where(l => l.EntityName == entity)
            .Select(l => Guid.TryParse(l.EntityId, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).Distinct().ToList();

        var products = Ids("Product");
        if (products.Count > 0)
            foreach (var p in await db.Products.IgnoreQueryFilters().Where(p => products.Contains(p.Id)).Select(p => new { p.Id, p.Name, p.Sku }).ToListAsync(ct))
                result[("Product", p.Id.ToString())] = $"{p.Sku} — {p.Name}";
        var customers = Ids("Customer");
        if (customers.Count > 0)
            foreach (var c in await db.Customers.IgnoreQueryFilters().Where(c => customers.Contains(c.Id)).Select(c => new { c.Id, c.Name }).ToListAsync(ct))
                result[("Customer", c.Id.ToString())] = c.Name;
        var categories = Ids("Category");
        if (categories.Count > 0)
            foreach (var c in await db.Categories.IgnoreQueryFilters().Where(c => categories.Contains(c.Id)).Select(c => new { c.Id, c.Name }).ToListAsync(ct))
                result[("Category", c.Id.ToString())] = c.Name;
        var suppliers = Ids("Supplier");
        if (suppliers.Count > 0)
            foreach (var s in await db.Suppliers.IgnoreQueryFilters().Where(s => suppliers.Contains(s.Id)).Select(s => new { s.Id, s.CompanyName, s.TradeName }).ToListAsync(ct))
                result[("Supplier", s.Id.ToString())] = s.TradeName ?? s.CompanyName;
        var orders = Ids("SalesOrder");
        if (orders.Count > 0)
            foreach (var o in await db.SalesOrders.Where(o => orders.Contains(o.Id)).Select(o => new { o.Id, o.Number }).ToListAsync(ct))
                result[("SalesOrder", o.Id.ToString())] = $"PED-{o.Number:D6}";
        foreach (var l in logs.Where(l => l.EntityName == "Usuario"))
            result[(l.EntityName, l.EntityId)] = l.EntityId;
        return result;
    }

    internal static IReadOnlyList<FieldChange> Diff(AuditLog log)
    {
        var before = Parse(log.OldValues);
        var after = Parse(log.NewValues);
        return before.Keys.Union(after.Keys)
            .Select(k => new FieldChange(FieldLabels.GetValueOrDefault(k, k), before.GetValueOrDefault(k), after.GetValueOrDefault(k)))
            .ToList();
    }

    private static Dictionary<string, string?> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => "Sim",
            JsonValueKind.False => "Não",
            JsonValueKind.Number => p.Value.GetDecimal().ToString("0.####", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")),
            JsonValueKind.String when p.Value.TryGetDateTimeOffset(out var dt) && p.Value.GetString()!.Length > 18
                => BrazilTime.ToLocal(dt).ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")),
            _ => p.Value.ToString(),
        });
    }
}
