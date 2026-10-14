using PriceComparator.Application.Interfaces;
using PriceComparator.Application.UseCases.SearchProduct;

namespace PriceComparator.Application.UseCases;

public sealed record ProductUseCases(
    GetSearchProduct GetSearchProduct)
    : IScopedDependency;