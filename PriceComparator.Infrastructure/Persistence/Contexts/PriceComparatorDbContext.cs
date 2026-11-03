using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PriceComparator.Domain.Common;
using PriceComparator.Infrastructure.Persistence.Configurations;

namespace PriceComparator.Infrastructure.Persistence.Contexts;

public sealed class PriceComparatorDbContext : DbContext
{
    private readonly IConfiguration _configuration;

    public PriceComparatorDbContext(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection")
                                   ?? throw new InvalidOperationException(
                                       "No se encontró la cadena de conexión DefaultConnection.");

            optionsBuilder.UseNpgsql(
                connectionString,
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsHistoryTable(
                        "__EFMigrationsHistory",
                        "PriceComparator"
                    );
                });
        }

        optionsBuilder
            .UseSeeding((context, _) =>
            {
                ConceptSeed.Seed(context);
            })
            .UseAsyncSeeding(async (context, _, cancellationToken) =>
            {
                await ConceptSeed.SeedAsync(
                    context,
                    cancellationToken);
            });
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("PriceComparator");

        modelBuilder.RegisterEntities(
            typeof(Entity<>).Assembly,
            typeof(EntityRegistration).Assembly);
    }
}