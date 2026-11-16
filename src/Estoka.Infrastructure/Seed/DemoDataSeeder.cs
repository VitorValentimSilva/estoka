using System.Text.Json;
using Estoka.Application.Common;
using Estoka.Domain.Auditing;
using Estoka.Domain.Catalog;
using Estoka.Domain.Common;
using Estoka.Domain.Customers;
using Estoka.Domain.Inventory;
using Estoka.Domain.Sales;
using Estoka.Infrastructure.Persistence;
using Estoka.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Estoka.Infrastructure.Seed;

/// <summary>
/// Gera dados fictícios e realistas da "Distribuidora Bom Preço" com datas relativas a hoje.
/// Determinístico (semente fixa) e idempotente: só roda com o banco vazio, ou via <see cref="ResetAsync"/>.
/// Pedidos e movimentações são produzidos pelas próprias regras de domínio, então o estoque final
/// é exatamente a soma das movimentações.
/// </summary>
public sealed class DemoDataSeeder(IDbContextFactory<AppDbContext> factory, TimeProvider clock, ILogger<DemoDataSeeder> logger)
{
    private const int Seed = 20_24;
    private static readonly string[] Sellers = ["Juliana Costa", "Ricardo Lima", "Carlos Mendes", "Patrícia Gomes"];

    public async Task<bool> SeedIfEmptyAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (await db.Products.IgnoreQueryFilters().AnyAsync(ct) || await db.Customers.IgnoreQueryFilters().AnyAsync(ct))
            return false;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await GenerateAsync(db, ct);
        await tx.CommitAsync(ct);
        return true;
    }

    /// <summary>
    /// Bases criadas antes do código de barras: preenche o EAN dos produtos de demonstração (SKU "MER-002" etc.)
    /// que ainda não têm, sem esperar o reset diário. Idempotente.
    /// </summary>
    public async Task<int> BackfillDemoBarcodesAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.SuppressAudit = true;
        var prefixes = DemoCatalog.Categories.Select((c, i) => (c.Prefix, Index: i + 1)).ToDictionary(x => x.Prefix, x => x.Index);
        var pending = await db.Products.IgnoreQueryFilters().Where(p => p.Barcode == null).ToListAsync(ct);
        var updated = 0;
        foreach (var product in pending)
        {
            var parts = product.Sku.Split('-');
            if (parts.Length != 2 || !prefixes.TryGetValue(parts[0], out var categoryIndex)
                || !int.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var number)) continue;
            var barcode = DemoBarcode(categoryIndex, number);
            if (await db.Products.IgnoreQueryFilters().AnyAsync(p => p.Barcode == barcode, ct)) continue;
            product.SetBarcode(barcode);
            updated++;
        }
        if (updated > 0) await db.SaveChangesAsync(ct);
        return updated;
    }

    /// <summary>Apaga todos os dados de negócio e gera novamente, numa única transação (usuários são mantidos).</summary>
    public async Task ResetAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE sales_order_items, stock_movements, sales_orders, products, customers, suppliers, categories, audit_logs
            RESTART IDENTITY CASCADE;
            ALTER SEQUENCE sales_order_number_seq RESTART WITH 1001;
            """, ct);
        await GenerateAsync(db, ct);
        await tx.CommitAsync(ct);
    }

    private async Task GenerateAsync(AppDbContext db, CancellationToken ct)
    {
        var started = clock.GetTimestamp();
        db.SuppressAudit = true;
        db.ChangeTracker.AutoDetectChangesEnabled = true;
        var rnd = new Random(Seed);
        var today = BrazilTime.Today(clock);
        var now = clock.GetUtcNow();
        DateTimeOffset At(DateOnly day, double hour)
        {
            var value = BrazilTime.StartOfDayUtc(day).AddHours(hour);
            return value > now ? now.AddMinutes(-rnd.Next(5, 90)) : value;
        }

        var setupDay = today.AddDays(-95);

        // Fornecedores
        var suppliers = DemoCatalog.Suppliers.Select(s =>
        {
            var supplier = new Supplier(s.CompanyName, s.TradeName, GenerateCnpj(rnd), Slug(s.TradeName) + "@fornecedor.example",
                Phone(rnd, 19), s.Contact, s.City, s.State);
            supplier.CreatedAt = At(setupDay, 9);
            return supplier;
        }).ToList();
        db.Suppliers.AddRange(suppliers);

        // Categorias e produtos
        var products = new List<Product>();
        var categoryIndex = 0;
        foreach (var c in DemoCatalog.Categories)
        {
            categoryIndex++;
            var category = new Category(c.Name, c.Description) { CreatedAt = At(setupDay, 9.5) };
            db.Categories.Add(category);
            var n = 1;
            foreach (var p in c.Products)
            {
                var markup = 1.22m + (decimal)rnd.NextDouble() * 0.38m;
                var sale = RoundPrice(p.Cost * markup);
                var minimum = p.Cost switch { < 5 => 40, < 15 => 25, < 40 => 12, _ => 6 };
                var supplier = rnd.NextDouble() < 0.8 ? suppliers[c.SupplierIndex] : suppliers[8 + rnd.Next(2)];
                var product = new Product($"{c.Prefix}-{n++:000}", p.Name, null, category.Id, supplier.Id,
                    p.Cost, sale, p.Unit, minimum) { CreatedAt = At(setupDay, 10) };
                product.SetBarcode(DemoBarcode(categoryIndex, n - 1));
                products.Add(product);
            }
        }
        db.Products.AddRange(products);

        // Popularidade (distribuição de Pareto → curva ABC realista)
        var shuffled = products.OrderBy(_ => rnd.Next()).ToList();
        var weights = shuffled.ToDictionary(p => p.Id, p => 1.0 / Math.Pow(shuffled.IndexOf(p) + 1, 0.85));
        // Alguns produtos sem reposição recente, para gerar alertas de estoque baixo
        var neglected = shuffled.Skip(3).Where((_, i) => i % 11 == 0).Take(9).Select(p => p.Id).ToHashSet();
        shuffled[^1].Deactivate();
        shuffled[^2].Deactivate();

        // Clientes
        var customers = new List<Customer>();
        for (var i = 0; i < 35; i++)
        {
            var name = $"{Pick(rnd, DemoCatalog.FirstNames)} {Pick(rnd, DemoCatalog.LastNames)} {Pick(rnd, DemoCatalog.LastNames)}";
            customers.Add(new Customer(PersonType.PF, name, GenerateCpf(rnd), Slug(name) + "@email.example",
                Phone(rnd, 19, mobile: true), RandomAddress(rnd)) { CreatedAt = At(setupDay.AddDays(rnd.Next(0, 30)), 11) });
        }
        foreach (var business in DemoCatalog.BusinessNames.Take(25))
        {
            var suffix = rnd.NextDouble() < 0.5 ? " Ltda" : " ME";
            customers.Add(new Customer(PersonType.PJ, business + suffix, GenerateCnpj(rnd), "compras@" + Slug(business) + ".example",
                Phone(rnd, 19), RandomAddress(rnd)) { CreatedAt = At(setupDay.AddDays(rnd.Next(0, 30)), 14) });
        }
        db.Customers.AddRange(customers);
        var customerWeights = customers.ToDictionary(c => c.Id, c => c.Type == PersonType.PJ ? 3.0 : 1.0);

        // Estoque inicial
        foreach (var p in products)
        {
            var qty = Math.Round(p.MinimumStock * (decimal)(3 + rnd.NextDouble() * 3));
            db.StockMovements.Add(p.RegisterMovement(StockMovementType.Entrada, qty, "Estoque inicial (implantação)",
                "Mariana Alves", At(setupDay, 8), p.CostPrice));
        }
        await db.SaveChangesAsync(ct);

        var invoice = 48_210;
        var orderCount = 0;
        for (var day = today.AddDays(-90); day <= today; day = day.AddDays(1))
        {
            // Reposição semanal (segunda-feira)
            if (day.DayOfWeek == DayOfWeek.Monday)
            {
                foreach (var p in products.Where(p => p.IsActive && p.CurrentStock < p.MinimumStock * 2))
                {
                    if (neglected.Contains(p.Id) && day > today.AddDays(-40)) continue;
                    var qty = Math.Round(p.MinimumStock * (decimal)(3 + rnd.NextDouble() * 2));
                    var cost = Math.Round(p.CostPrice * (decimal)(0.98 + rnd.NextDouble() * 0.05), 2);
                    db.StockMovements.Add(p.RegisterMovement(StockMovementType.Entrada, qty, $"Compra — NF-e {invoice++}",
                        "Ricardo Lima", At(day, 7.5 + rnd.NextDouble()), cost));
                }
            }

            var ordersToday = day.DayOfWeek switch
            {
                DayOfWeek.Sunday => rnd.Next(0, 2),
                DayOfWeek.Saturday => rnd.Next(1, 4),
                _ => rnd.Next(2, 6),
            };

            for (var o = 0; o < ordersToday; o++)
            {
                var customer = WeightedPick(rnd, customers, c => customerWeights[c.Id]);
                var orderAt = At(day, 8 + rnd.NextDouble() * 10);
                var age = today.DayNumber - day.DayNumber;
                var status = PickStatus(rnd, age);
                var payment = customer.Type == PersonType.PJ
                    ? Pick(rnd, [PaymentMethod.Boleto, PaymentMethod.APrazo, PaymentMethod.Pix, PaymentMethod.Boleto])
                    : Pick(rnd, [PaymentMethod.Pix, PaymentMethod.CartaoCredito, PaymentMethod.CartaoDebito, PaymentMethod.Dinheiro]);

                var order = new SalesOrder(customer.Id, orderAt, payment, Pick(rnd, Sellers)) { CreatedAt = orderAt };
                var itemCount = rnd.Next(1, customer.Type == PersonType.PJ ? 8 : 5);
                var chosen = new HashSet<Guid>();
                var needsStock = status is not (SalesOrderStatus.Rascunho or SalesOrderStatus.Cancelado) || rnd.NextDouble() < 0.5;
                for (var i = 0; i < itemCount * 3 && chosen.Count < itemCount; i++)
                {
                    var p = WeightedPick(rnd, products, x => weights[x.Id]);
                    if (!p.IsActive || !chosen.Add(p.Id)) continue;
                    decimal qty = customer.Type == PersonType.PJ ? rnd.Next(2, 16) : rnd.Next(1, 6);
                    if (p.Unit == UnitOfMeasure.Quilograma) qty = Math.Round((decimal)(0.5 + rnd.NextDouble() * 4), 1);
                    if (needsStock) qty = Math.Min(qty, Math.Floor(p.CurrentStock));
                    if (qty <= 0) { chosen.Remove(p.Id); continue; }
                    var discount = rnd.NextDouble() < 0.15 ? Math.Round(qty * p.SalePrice * 0.05m, 2) : 0;
                    order.AddItem(p, qty, p.SalePrice, discount);
                }
                if (order.Items.Count == 0) continue;
                if (customer.Type == PersonType.PJ && order.Subtotal > 800 && rnd.NextDouble() < 0.3)
                    order.SetDiscount(Math.Round(order.Subtotal * 0.03m, 2));

                db.SalesOrders.Add(order);
                orderCount++;
                if (status == SalesOrderStatus.Rascunho) continue;

                if (status == SalesOrderStatus.Cancelado && !needsStock)
                {
                    order.Cancel(orderAt.AddHours(2), "Cliente desistiu da compra");
                    continue;
                }

                var confirmedAt = orderAt.AddMinutes(rnd.Next(10, 120));
                order.Confirm(confirmedAt);
                foreach (var item in order.Items)
                {
                    var product = products.First(p => p.Id == item.ProductId);
                    db.StockMovements.Add(product.RegisterMovement(StockMovementType.Saida, item.Quantity, $"Venda {CodePlaceholder}",
                        order.SellerName, confirmedAt, salesOrderId: order.Id));
                }

                if (status == SalesOrderStatus.Cancelado)
                {
                    var canceledAt = At(day.AddDays(1), 9 + rnd.NextDouble() * 6);
                    order.Cancel(canceledAt, Pick(rnd, ["Pagamento não aprovado", "Pedido duplicado", "Cliente solicitou cancelamento"]));
                    foreach (var item in order.Items)
                    {
                        var product = products.First(p => p.Id == item.ProductId);
                        db.StockMovements.Add(product.RegisterMovement(StockMovementType.Devolucao, item.Quantity,
                            $"Cancelamento {CodePlaceholder}", "Ricardo Lima", canceledAt, salesOrderId: order.Id));
                    }
                    continue;
                }

                if (status is SalesOrderStatus.Faturado or SalesOrderStatus.Entregue)
                    order.Invoice(At(day.AddDays(age > 0 ? 1 : 0), 10 + rnd.NextDouble() * 4));
                if (status == SalesOrderStatus.Entregue)
                    order.Deliver(At(day.AddDays(Math.Min(age, 2)), 15 + rnd.NextDouble() * 3));
            }

            await db.SaveChangesAsync(ct);
        }

        // Inventário recente: perdas/avarias deixam alguns produtos abaixo do mínimo (alertas no dashboard)
        foreach (var p in products.Where(p => neglected.Contains(p.Id) && p.IsActive))
        {
            if (p.CurrentStock < p.MinimumStock) continue;
            var target = Math.Floor(p.MinimumStock * (decimal)(0.2 + rnd.NextDouble() * 0.5));
            db.StockMovements.Add(p.RegisterMovement(StockMovementType.Ajuste, target - p.CurrentStock,
                "Inventário: avaria e perdas identificadas", "Ricardo Lima", At(today.AddDays(-1), 17)));
        }

        AddSampleAuditTrail(db, products, customers, rnd, At, today);
        await db.SaveChangesAsync(ct);

        // As movimentações referenciam o pedido; o número só existe após o INSERT (sequence), então ajustamos o texto.
        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE stock_movements m
               SET reason = replace(m.reason, {0}, 'PED-' || lpad(o.number::text, 6, '0'))
              FROM sales_orders o
             WHERE m.sales_order_id = o.id AND m.reason LIKE {1};
            """, [CodePlaceholder, "%" + CodePlaceholder], ct);

        await MarkResetAsync(db, ct);
        db.SuppressAudit = false;
        logger.LogInformation("Dados de demonstração gerados: {Products} produtos, {Customers} clientes, {Orders} pedidos em {Elapsed:0.0}s",
            products.Count, customers.Count, orderCount, clock.GetElapsedTime(started).TotalSeconds);
    }

    private const string CodePlaceholder = "#PEDIDO#";

    /// <summary>Data/hora (UTC) da última geração dos dados de demonstração, ou null se nunca registrada.</summary>
    public async Task<DateTimeOffset?> GetLastResetAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var value = await db.SystemSettings.Where(s => s.Key == SystemSetting.LastDemoResetKey).Select(s => s.Value).FirstOrDefaultAsync(ct);
        return DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var at)
            ? at
            : null;
    }

    private async Task MarkResetAsync(AppDbContext db, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var setting = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == SystemSetting.LastDemoResetKey, ct);
        if (setting is null)
            db.SystemSettings.Add(new SystemSetting { Key = SystemSetting.LastDemoResetKey, Value = now.ToString("O"), UpdatedAt = now });
        else
        {
            setting.Value = now.ToString("O");
            setting.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);
    }

    private static void AddSampleAuditTrail(AppDbContext db, List<Product> products, List<Customer> customers, Random rnd,
        Func<DateOnly, double, DateTimeOffset> at, DateOnly today)
    {
        var logs = new List<AuditLog>();
        foreach (var (p, i) in products.Where(p => p.IsActive).OrderBy(_ => rnd.Next()).Take(8).Select((p, i) => (p, i)))
        {
            var old = RoundPrice(p.SalePrice * 0.94m);
            logs.Add(new AuditLog
            {
                EntityName = nameof(Product), EntityId = p.Id.ToString(), Action = AuditAction.Alterado,
                OldValues = JsonSerializer.Serialize(new Dictionary<string, object> { ["SalePrice"] = old }, AuditSaveChangesInterceptor.JsonOptions),
                NewValues = JsonSerializer.Serialize(new Dictionary<string, object> { ["SalePrice"] = p.SalePrice }, AuditSaveChangesInterceptor.JsonOptions),
                UserName = i % 3 == 0 ? "Mariana Alves" : "Ricardo Lima",
                Timestamp = at(today.AddDays(-rnd.Next(1, 20)), 9 + rnd.NextDouble() * 8),
            });
        }
        foreach (var c in customers.OrderBy(_ => rnd.Next()).Take(4))
        {
            logs.Add(new AuditLog
            {
                EntityName = nameof(Customer), EntityId = c.Id.ToString(), Action = AuditAction.Alterado,
                OldValues = JsonSerializer.Serialize(new Dictionary<string, object?> { ["Phone"] = null }, AuditSaveChangesInterceptor.JsonOptions),
                NewValues = JsonSerializer.Serialize(new Dictionary<string, object?> { ["Phone"] = c.Phone }, AuditSaveChangesInterceptor.JsonOptions),
                UserName = "Juliana Costa",
                Timestamp = at(today.AddDays(-rnd.Next(1, 15)), 10 + rnd.NextDouble() * 6),
            });
        }
        db.AuditLogs.AddRange(logs);
    }

    private static SalesOrderStatus PickStatus(Random rnd, int age)
    {
        var r = rnd.NextDouble();
        return age switch
        {
            <= 1 => r switch { < 0.35 => SalesOrderStatus.Rascunho, < 0.75 => SalesOrderStatus.Confirmado, < 0.92 => SalesOrderStatus.Faturado, _ => SalesOrderStatus.Cancelado },
            <= 6 => r switch { < 0.12 => SalesOrderStatus.Confirmado, < 0.40 => SalesOrderStatus.Faturado, < 0.92 => SalesOrderStatus.Entregue, _ => SalesOrderStatus.Cancelado },
            _ => r switch { < 0.03 => SalesOrderStatus.Faturado, < 0.90 => SalesOrderStatus.Entregue, _ => SalesOrderStatus.Cancelado },
        };
    }

    private static Address RandomAddress(Random rnd)
    {
        var (city, districts) = DemoCatalog.Cities[rnd.NextDouble() < 0.55 ? 0 : rnd.Next(1, DemoCatalog.Cities.Length)];
        return new Address($"13{rnd.Next(0, 999999):000000}", Pick(rnd, DemoCatalog.Streets), rnd.Next(10, 3500).ToString(System.Globalization.CultureInfo.InvariantCulture),
            rnd.NextDouble() < 0.2 ? $"Sala {rnd.Next(1, 40)}" : null, Pick(rnd, districts), city, "SP");
    }

    /// <summary>
    /// EAN-13 fictício e determinístico: prefixo GS1 "20" (faixa reservada para uso interno/loja, sem colisão
    /// com produtos reais) + "4512" + categoria (2 dígitos) + item (4 dígitos) + dígito verificador.
    /// </summary>
    public static string DemoBarcode(int categoryIndex, int itemNumber)
    {
        var body = $"204512{categoryIndex:00}{itemNumber:0000}";
        return body + Gtin.CheckDigit(body);
    }

    public static string GenerateCpf(Random rnd)
    {
        var digits = string.Concat(Enumerable.Range(0, 9).Select(_ => rnd.Next(10)));
        digits += DocumentValidator.CpfDigit(digits, 9);
        digits += DocumentValidator.CpfDigit(digits, 10);
        return DocumentValidator.IsValidCpf(digits) ? digits : GenerateCpf(rnd);
    }

    public static string GenerateCnpj(Random rnd)
    {
        var digits = string.Concat(Enumerable.Range(0, 8).Select(_ => rnd.Next(10))) + "0001";
        digits += DocumentValidator.CnpjDigit(digits, 12);
        digits += DocumentValidator.CnpjDigit(digits, 13);
        return DocumentValidator.IsValidCnpj(digits) ? digits : GenerateCnpj(rnd);
    }

    private static string Phone(Random rnd, int ddd, bool mobile = false)
        => mobile ? $"{ddd}9{rnd.Next(8000_0000, 9999_9999)}" : $"{ddd}3{rnd.Next(100_0000, 999_9999)}";

    private static T Pick<T>(Random rnd, IReadOnlyList<T> items) => items[rnd.Next(items.Count)];

    private static T WeightedPick<T>(Random rnd, IReadOnlyList<T> items, Func<T, double> weight)
    {
        var total = items.Sum(weight);
        var target = rnd.NextDouble() * total;
        foreach (var item in items)
        {
            target -= weight(item);
            if (target <= 0) return item;
        }
        return items[^1];
    }

    private static decimal RoundPrice(decimal value) => Math.Ceiling(value * 10) / 10 - 0.01m;

    private static string Slug(string value)
    {
        var normalized = value.Normalize(System.Text.NormalizationForm.FormD);
        var chars = normalized.Where(c => char.IsAsciiLetterOrDigit(c) || c == ' ').ToArray();
        return string.Join('.', new string(chars).ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(3));
    }
}
