using PriceComparator.Domain.Entities;

public sealed record CheapestStoreTotalModel(
    string Store,
    string SellerName,
    int TotalCanonicalEconomic,
    IReadOnlyCollection<CheapestCanonicalProduct> Canonical
);

public sealed record CheapestCanonicalProduct(
    string CanonicalKey,
    string? CanonicalName,
    Product Product
);