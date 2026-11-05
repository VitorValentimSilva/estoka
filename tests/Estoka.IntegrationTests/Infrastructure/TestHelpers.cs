using Estoka.Application.Catalog;
using Estoka.Application.Customers;
using Estoka.Application.Security;
using Estoka.Domain.Customers;
using Estoka.Infrastructure.Seed;
using Microsoft.Extensions.DependencyInjection;

namespace Estoka.IntegrationTests.Infrastructure;

public static class TestHelpers
{
    private static int _counter;

    /// <summary>Cria um produto novo (SKU único) com o estoque informado, via serviço, como Gerente.</summary>
    public static async Task<Guid> CreateProductAsync(this EstokaFactory factory, decimal stock, decimal price = 10m, decimal cost = 6m)
    {
        await using var scope = factory.ScopeAs(Roles.Gerente);
        var categories = await scope.ServiceProvider.GetRequiredService<CategoryService>().LookupAsync();
        var n = Interlocked.Increment(ref _counter);
        return await scope.ServiceProvider.GetRequiredService<ProductService>().CreateAsync(new ProductInput
        {
            Sku = $"TST-{Guid.NewGuid():N}"[..20],
            Name = $"Produto de teste {n}",
            CategoryId = categories[0].Id,
            CostPrice = cost,
            SalePrice = price,
            MinimumStock = 1,
            InitialStock = stock,
        });
    }

    public static async Task<Guid> CreateCustomerAsync(this EstokaFactory factory)
    {
        await using var scope = factory.ScopeAs(Roles.Vendedor);
        return await scope.ServiceProvider.GetRequiredService<CustomerService>().CreateAsync(new CustomerInput
        {
            Type = PersonType.PF,
            Name = "Cliente de Teste",
            Document = DemoDataSeeder.GenerateCpf(Random.Shared),
            Email = "cliente@teste.example",
        });
    }
}
