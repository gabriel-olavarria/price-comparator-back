using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceComparator.Domain.Entities;

public class PackagingTermAliasConfiguration : IEntityTypeConfiguration<PackagingTermAlias>
{
    public void Configure(EntityTypeBuilder<PackagingTermAlias> builder)
    {
        builder.ToTable("PackagingTermAliases", "PriceComparator");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.HasIndex(x => new
        {
            x.PackagingTermId,
            x.Name
        }).IsUnique();

        builder.HasData(
            new PackagingTermAlias { Id = 1, PackagingTermId = 5, Name = "no-retor" },
            new PackagingTermAlias { Id = 2, PackagingTermId = 5, Name = "no-retornable" },
            new PackagingTermAlias { Id = 3, PackagingTermId = 5, Name = "no retornable" },
            new PackagingTermAlias { Id = 4, PackagingTermId = 5, Name = "desechable" },
            new PackagingTermAlias { Id = 5, PackagingTermId = 7, Name = "retor" },
            new PackagingTermAlias { Id = 6, PackagingTermId = 7, Name = "retornable" },
            new PackagingTermAlias { Id = 7, PackagingTermId = 5, Name = "pet" }
       
        );

        builder.HasOne(x => x.PackagingTerm)
            .WithMany(x => x.Aliases)
            .HasForeignKey(x => x.PackagingTermId)
            .OnDelete(DeleteBehavior.Cascade);
        
       
    }
}