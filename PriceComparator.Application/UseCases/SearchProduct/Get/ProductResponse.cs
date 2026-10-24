using PriceComparator.Domain.Entities;

namespace PriceComparator.Application.UseCases.SearchProduct.Get;

public sealed record SearchResponse(
    string Query,
    int TotalResults,
    IReadOnlyDictionary<string, IReadOnlyCollection<Product>> Stores);