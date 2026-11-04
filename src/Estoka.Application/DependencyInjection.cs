using Estoka.Application.Catalog;
using Estoka.Application.Customers;
using Estoka.Application.Dashboard;
using Estoka.Application.Inventory;
using Estoka.Application.Sales;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Estoka.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ValidatorOptions.Global.LanguageManager.Culture = new System.Globalization.CultureInfo("pt-BR");
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, ServiceLifetime.Singleton);

        services.AddScoped<CategoryService>();
        services.AddScoped<SupplierService>();
        services.AddScoped<ProductService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<StockService>();
        services.AddScoped<SalesOrderService>();
        services.AddScoped<DashboardService>();
        return services;
    }
}
