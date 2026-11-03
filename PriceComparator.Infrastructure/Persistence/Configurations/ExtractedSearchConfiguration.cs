using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceComparator.Domain.Entities;

namespace PriceComparator.Infrastructure.Persistence.Configurations;

public sealed class ExtractedSearchConfiguration
    : IEntityTypeConfiguration<ExtractedSearch>
{
    public void Configure(EntityTypeBuilder<ExtractedSearch> builder)
    {
        builder.HasIndex(x => new { x.Store, x.SearchQuery })
            .IsUnique();

        builder.Property(x => x.Products)
            .HasColumnType("jsonb");
    }
}