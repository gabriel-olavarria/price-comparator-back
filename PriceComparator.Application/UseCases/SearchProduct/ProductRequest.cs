namespace PriceComparator.Application.UseCases.SearchProduct;

public sealed record ProductRequest(string? Query, IReadOnlyCollection<string> Categories);