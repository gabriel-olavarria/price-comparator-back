using PriceComparator.Domain.Entities;

namespace PriceComparator.Domain.Models;

public sealed record RankedProduct(
    Product Product,
    int Score,
    ProductScoreDebug Debug);