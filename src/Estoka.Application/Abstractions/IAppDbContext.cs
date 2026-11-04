using Estoka.Domain.Auditing;
using Estoka.Domain.Catalog;
using Estoka.Domain.Customers;
using Estoka.Domain.Inventory;
using Estoka.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Estoka.Application.Abstractions;

/// <summary>
/// Porta de acesso a dados usada pelos casos de uso. Expõe os DbSets do EF Core diretamente
/// (sem repositório genérico): o DbContext já é Unit of Work + Repository.
/// </summary>
public interface IAppDbContext : IAsyncDisposable, IDisposable
{
    DbSet<Category> Categories { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<Product> Products { get; }
    DbSet<Customer> Customers { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<SalesOrder> SalesOrders { get; }
    DbSet<SalesOrderItem> SalesOrderItems { get; }
    DbSet<AuditLog> AuditLogs { get; }

    DatabaseFacade Database { get; }
    ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Cria um contexto por operação. No Blazor Server o escopo de DI dura o circuito inteiro,
/// então um DbContext por operação evita contexto compartilhado e dados obsoletos.
/// </summary>
public interface IAppDbContextFactory
{
    Task<IAppDbContext> CreateAsync(CancellationToken cancellationToken = default);
}
