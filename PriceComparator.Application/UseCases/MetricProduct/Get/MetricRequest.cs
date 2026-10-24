using PriceComparator.Domain.Entities;

namespace PriceComparator.Application.UseCases.MetricProduct.Get;

public sealed record MetricRequest(
    IReadOnlyDictionary<string, IReadOnlyCollection<Product>> Stores
);


