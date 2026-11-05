
using PriceComparator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PriceComparator.Infrastructure.Persistence.Configurations;


public sealed class PackagingConfiguration : IEntityTypeConfiguration<Packaging>
{
    public void Configure(EntityTypeBuilder<Packaging> builder)
    {
        builder.ToTable("Packaging");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(x => x.Name)
            .IsUnique();

        builder.HasMany(x => x.Terms)
            .WithOne(x => x.Packaging)
            .HasForeignKey(x => x.PackagingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasData(
            new Packaging { Id = 1, Name = "Lata" },
            new Packaging { Id = 2, Name = "Botella" }
        );
    }
}