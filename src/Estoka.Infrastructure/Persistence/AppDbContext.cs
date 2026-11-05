using Estoka.Application.Abstractions;
using Estoka.Application.Common;
using Estoka.Domain.Auditing;
using Estoka.Domain.Catalog;
using Estoka.Domain.Common;
using Estoka.Domain.Customers;
using Estoka.Domain.Inventory;
using Estoka.Domain.Sales;
using Estoka.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Estoka.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole, string>(options), IAppDbContext, IDataProtectionKeyContext
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderItem> SalesOrderItems => Set<SalesOrderItem>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    /// <summary>Desliga a geração de AuditLog (usado na carga em massa da demonstração).</summary>
    public bool SuppressAudit { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasPostgresExtension("pg_trgm");
        builder.HasSequence<long>(Configurations.SalesOrderConfiguration.NumberSequence).StartsAt(1001);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        builder.Entity<AppUser>(b =>
        {
            b.ToTable("users");
            b.Property(u => u.FullName).HasMaxLength(150);
        });
        builder.Entity<IdentityRole>().ToTable("roles");
        builder.Entity<IdentityUserRole<string>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<string>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<string>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<string>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<string>>().ToTable("role_claims");
        builder.Entity<DataProtectionKey>().ToTable("data_protection_keys");
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Dinheiro: numeric(18,2). Quantidades sobrescrevem para (18,3) nas configurações.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            throw new DomainException(UniqueViolationMessage(pg.ConstraintName));
        }
    }

    private static string UniqueViolationMessage(string? constraint) => constraint switch
    {
        { } c when c.Contains("sku", StringComparison.Ordinal) => "Já existe um produto com este SKU.",
        { } c when c.Contains("document", StringComparison.Ordinal) => "Já existe um cliente com este documento.",
        { } c when c.Contains("cnpj", StringComparison.Ordinal) => "Já existe um fornecedor com este CNPJ.",
        { } c when c.Contains("name", StringComparison.Ordinal) => "Já existe um registro com este nome.",
        _ => "Registro duplicado.",
    };
}
