using PriceComparator.Domain.Entities;

namespace PriceComparator.Application.Models;

public sealed record RankedProduct(Product Product, int Score, ProductScoreDebug Debug);