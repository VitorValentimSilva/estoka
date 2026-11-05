using Estoka.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estoka.Infrastructure.Persistence.Configurations;

internal static class ConfigurationExtensions
{
    public static void ConfigureAuditColumns<T>(this EntityTypeBuilder<T> b) where T : Domain.Common.AuditableEntity
    {
        b.Property(x => x.CreatedBy).HasMaxLength(150);
        b.Property(x => x.UpdatedBy).HasMaxLength(150);
    }

    public static void ConfigureSoftDelete<T>(this EntityTypeBuilder<T> b) where T : Domain.Common.SoftDeletableEntity
    {
        b.ConfigureAuditColumns();
        b.Property(x => x.DeletedBy).HasMaxLength(150);
        b.HasQueryFilter(x => !x.IsDeleted);
    }

    public static PropertyBuilder<decimal> IsQuantity(this PropertyBuilder<decimal> p) => p.HasPrecision(18, 3);
}

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable("categories");
        b.ConfigureSoftDelete();
        b.Property(x => x.Name).HasMaxLength(Category.NameMaxLength).IsRequired();
        b.Property(x => x.Description).HasMaxLength(Category.DescriptionMaxLength);
        b.HasIndex(x => x.Name).IsUnique().HasFilter("is_deleted = false");
    }
}

internal sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b)
    {
        b.ToTable("suppliers");
        b.ConfigureSoftDelete();
        b.Property(x => x.CompanyName).HasMaxLength(150).IsRequired();
        b.Property(x => x.TradeName).HasMaxLength(150);
        b.Property(x => x.Cnpj).HasMaxLength(14).IsFixedLength().IsRequired();
        b.Property(x => x.Email).HasMaxLength(150);
        b.Property(x => x.Phone).HasMaxLength(11);
        b.Property(x => x.ContactName).HasMaxLength(100);
        b.Property(x => x.City).HasMaxLength(80);
        b.Property(x => x.State).HasMaxLength(2).IsFixedLength();
        b.Property(x => x.Notes).HasMaxLength(500);
        b.Ignore(x => x.DisplayName);
        b.HasIndex(x => x.Cnpj).IsUnique().HasFilter("is_deleted = false");
        b.HasIndex(x => x.CompanyName).HasMethod("gin").HasOperators("gin_trgm_ops");
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("products", t =>
        {
            t.HasCheckConstraint("ck_products_current_stock_not_negative", "current_stock >= 0");
            t.HasCheckConstraint("ck_products_prices_not_negative", "cost_price >= 0 AND sale_price >= 0");
        });
        b.ConfigureSoftDelete();
        b.Property(x => x.Sku).HasMaxLength(Product.SkuMaxLength).IsRequired();
        b.Property(x => x.Name).HasMaxLength(Product.NameMaxLength).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.ImageUrl).HasMaxLength(500);
        b.Property(x => x.Barcode).HasMaxLength(Product.BarcodeMaxLength);
        b.Property(x => x.Unit).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.CurrentStock).IsQuantity();
        b.Property(x => x.MinimumStock).IsQuantity();
        b.Property(x => x.Version).IsRowVersion(); // xmin do PostgreSQL
        b.Ignore(x => x.IsBelowMinimum);
        b.Ignore(x => x.MarginPercent);

        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.Sku).IsUnique().HasFilter("is_deleted = false");
        b.HasIndex(x => x.Barcode).IsUnique().HasFilter("barcode IS NOT NULL AND is_deleted = false");
        b.HasIndex(x => x.Name).HasMethod("gin").HasOperators("gin_trgm_ops");
        b.HasIndex(x => x.CategoryId);
        b.HasIndex(x => x.SupplierId);
    }
}
