
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PriceComparator.Domain.Entities;

namespace PriceComparator.Infrastructure.Persistence.Configurations;

public sealed class ProcessedSearchConfiguration
    : IEntityTypeConfiguration<ProcessedSearch>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<ProcessedSearch> builder)
    {
        builder.ToTable("ProcessedSearch");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Store)
            .IsRequired();

        builder.Property(x => x.SearchQuery)
            .IsRequired();

        builder.Property(x => x.ProductCount)
            .IsRequired();

        builder.Property(x => x.GeneratedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.Store,
            x.SearchQuery
        }).IsUnique();

        var converter =
            new ValueConverter<IReadOnlyCollection<Product>, string>(
                products => JsonSerializer.Serialize(
                    products,
                    JsonOptions),
                json => JsonSerializer.Deserialize<List<Product>>(
                    json,
                    JsonOptions) ?? new List<Product>());

        var comparer =
            new ValueComparer<IReadOnlyCollection<Product>>(
                (a, b) =>
                    JsonSerializer.Serialize(a, JsonOptions) ==
                    JsonSerializer.Serialize(b, JsonOptions),

                products =>
                    JsonSerializer.Serialize(products, JsonOptions)
                        .GetHashCode(),

                products =>
                    JsonSerializer.Deserialize<List<Product>>(
                        JsonSerializer.Serialize(products, JsonOptions),
                        JsonOptions) ?? new List<Product>());

        var productsProperty = builder.Property(x => x.Products)
            .HasConversion(converter)
            .HasColumnType("jsonb")
            .IsRequired();

        productsProperty.Metadata.SetValueComparer(comparer);
    }
}
