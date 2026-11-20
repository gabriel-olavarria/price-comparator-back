
using PriceComparator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class PackagingTermConfiguration : IEntityTypeConfiguration<PackagingTerm>
{
    public void Configure(EntityTypeBuilder<PackagingTerm> builder)
    {
        builder.ToTable("PackagingTerm");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(x => new
        {
            x.PackagingId,
            x.Name
        }).IsUnique();

        builder.HasData(
            new PackagingTerm { Id = 1, Name = "lata", PackagingId = 1 },
            new PackagingTerm { Id = 2, Name = "latas", PackagingId = 1 },

            new PackagingTerm { Id = 3, Name = "botella", PackagingId = 2 },
            new PackagingTerm { Id = 4, Name = "botellas", PackagingId = 2 },
            new PackagingTerm { Id = 5, Name = "desechable", PackagingId = 2 },
            new PackagingTerm { Id = 6, Name = "vidrio", PackagingId = 2 },
            new PackagingTerm { Id = 7, Name = "retornable", PackagingId = 2 }
        );
    }
}