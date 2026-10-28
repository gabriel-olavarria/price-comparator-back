using PriceComparator.Domain.Entities;

namespace PriceComparator.Application.UseCases.MetricProduct.Get;

public sealed record MetricResponse(
    IReadOnlyList<StoreProductTotal> TotalProductsByStore,
    IReadOnlyList<CanonicalProductTotal> TotalProductsByCanonicalKey,
    EconomicProduct? EconomicProduct,
    IReadOnlyList<CanonicalGroupResponse> TopCanonicalGroups,
    IReadOnlyList<CheapestStoreResponse> CheapestCanonicalStore
);

public sealed record StoreProductTotal(
    int TotalProducts,
    string Store
);

public sealed record CanonicalProductTotal(
    int TotalCanonicalKey,
    string Store
);

public sealed record EconomicProduct(
    string Name,
    decimal Price,
    string Store,
    Uri? ProductUrl,
    Uri? ImageUrl,
    string? CanonicalKey,
    string? CanonicalName
);

public sealed record CanonicalGroupResponse(
    string CanonicalKey,
    string? CanonicalName, 
    IReadOnlyList<EconomicProduct> Products
);

public sealed record CheapestStoreResponse(
    string Store,
    string SellerName,
    int TotalCanonicalEconomic,
    IReadOnlyList<CheapestCanonicalResponse> Canonical
);

public sealed record CheapestCanonicalResponse(
    string CanonicalKey,
    EconomicProduct Product
);