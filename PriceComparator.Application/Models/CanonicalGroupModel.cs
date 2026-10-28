using PriceComparator.Domain.Entities;

namespace PriceComparator.Application.Models;

public sealed record CanonicalGroupModel(
    string CanonicalKey, 
    string? CanonicalName, 
    IReadOnlyCollection<Product> Products);