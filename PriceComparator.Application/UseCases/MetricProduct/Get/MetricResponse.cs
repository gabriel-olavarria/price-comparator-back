using PriceComparator.Domain.Entities;

namespace PriceComparator.Application.UseCases.MetricProduct.Get;

public sealed record MetricResponse(
    IReadOnlyList<StoreProductTotal> TotalProductsByStore,
    IReadOnlyList<CanonicalProductTotal> TotalProductsByCanonicalKey,
    EconomicProduct? EconomicProduct,
    IReadOnlyList<CanonicalGroupResponse> TopCanonicalGroups
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
    string? CanonicalKey
);

public sealed record CanonicalGroupResponse(
    string CanonicalKey,
    IReadOnlyList<EconomicProduct> Products
);
