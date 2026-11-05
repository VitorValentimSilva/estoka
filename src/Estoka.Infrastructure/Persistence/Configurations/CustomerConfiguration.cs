using Estoka.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estoka.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("customers");
        b.ConfigureSoftDelete();
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(2);
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Document).HasMaxLength(14).IsRequired();
        b.Property(x => x.Email).HasMaxLength(150);
        b.Property(x => x.Phone).HasMaxLength(11);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.Ignore(x => x.FormattedDocument);

        b.ComplexProperty(x => x.Address, a =>
        {
            a.Property(p => p.ZipCode).HasMaxLength(8);
            a.Property(p => p.Street).HasMaxLength(150);
            a.Property(p => p.Number).HasMaxLength(20);
            a.Property(p => p.Complement).HasMaxLength(80);
            a.Property(p => p.District).HasMaxLength(80);
            a.Property(p => p.City).HasMaxLength(80);
            a.Property(p => p.State).HasMaxLength(2);
        });

        b.HasIndex(x => x.Document).IsUnique().HasFilter("is_deleted = false");
        b.HasIndex(x => x.Name).HasMethod("gin").HasOperators("gin_trgm_ops");
        b.HasIndex(x => x.Email);
    }
}
