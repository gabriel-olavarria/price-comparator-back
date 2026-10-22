using PriceComparator.Application.Interfaces.DependencyInjection;

namespace PriceComparator.Application.UseCases.SearchProduct.Get;

public sealed record ProductUseCases(GetSearchProduct GetSearchProduct) : IScopedDependency;