namespace PriceComparator.Application.UseCases.SearchProduct.Get;

public sealed record ProductOfferResult(
    string Name,
    decimal Price,
    IReadOnlyCollection<string> Categories,
    string ProductUrl,
    string? ImageUrl,
    string? Brand,
    string? SellerName);