using Estoka.Domain.Auditing;
using Estoka.Domain.Inventory;
using Estoka.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estoka.Infrastructure.Persistence.Configurations;

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.ToTable("stock_movements");
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Quantity).IsQuantity();
        b.Property(x => x.BalanceAfter).IsQuantity();
        b.Property(x => x.UnitCost).HasPrecision(18, 4);
        b.Property(x => x.Reason).HasMaxLength(200).IsRequired();
        b.Property(x => x.UserName).HasMaxLength(150);

        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SalesOrder).WithMany().HasForeignKey(x => x.SalesOrderId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.ProductId, x.OccurredAt });
        b.HasIndex(x => x.OccurredAt);
        b.HasIndex(x => x.SalesOrderId);
    }
}

internal sealed class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
{
    public const string NumberSequence = "sales_order_number_seq";

    public void Configure(EntityTypeBuilder<SalesOrder> b)
    {
        b.ToTable("sales_orders", t =>
            t.HasCheckConstraint("ck_sales_orders_total_not_negative", "total >= 0"));
        b.ConfigureAuditColumns();
        b.Property(x => x.Number).HasDefaultValueSql($"nextval('{NumberSequence}')").ValueGeneratedOnAdd();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.SellerName).HasMaxLength(150).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.CancelReason).HasMaxLength(300);
        b.Property(x => x.Version).IsRowVersion();
        b.Ignore(x => x.Code);
        b.Ignore(x => x.IsEditable);
        b.Ignore(x => x.HasStockCommitted);

        b.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.SalesOrderId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(x => x.Number).IsUnique();
        b.HasIndex(x => new { x.Status, x.OrderDate });
        b.HasIndex(x => x.OrderDate);
        b.HasIndex(x => x.CustomerId);
    }
}

internal sealed class SalesOrderItemConfiguration : IEntityTypeConfiguration<SalesOrderItem>
{
    public void Configure(EntityTypeBuilder<SalesOrderItem> b)
    {
        b.ToTable("sales_order_items");
        b.Property(x => x.ProductName).HasMaxLength(150).IsRequired();
        b.Property(x => x.Sku).HasMaxLength(30).IsRequired();
        b.Property(x => x.Quantity).IsQuantity();
        b.Property(x => x.UnitCost).HasPrecision(18, 4);
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.ProductId);
    }
}

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_logs");
        b.Property(x => x.Id).UseIdentityAlwaysColumn();
        b.Property(x => x.EntityName).HasMaxLength(80).IsRequired();
        b.Property(x => x.EntityId).HasMaxLength(64).IsRequired();
        b.Property(x => x.Action).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.OldValues).HasColumnType("jsonb");
        b.Property(x => x.NewValues).HasColumnType("jsonb");
        b.Property(x => x.UserName).HasMaxLength(150);
        b.HasIndex(x => new { x.EntityName, x.EntityId });
        b.HasIndex(x => x.Timestamp);
    }
}

