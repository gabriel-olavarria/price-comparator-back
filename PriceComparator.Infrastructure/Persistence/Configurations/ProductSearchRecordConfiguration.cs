using PriceComparator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class ProductSearchRecordConfiguration : IEntityTypeConfiguration<ProductSearchRecord>
{
    public void Configure(
        EntityTypeBuilder<ProductSearchRecord> builder)
    {
        builder.ToTable("ProductSearchRecord");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Store)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Query)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(x => x.SearchDate)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new
        {
            x.Store,
            x.Name,
            x.Query
        }).IsUnique();

        builder.HasIndex(x => x.SearchDate);
    }
}