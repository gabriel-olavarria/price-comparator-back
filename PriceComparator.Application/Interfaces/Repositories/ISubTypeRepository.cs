using PriceComparator.Domain.Entities;
using PriceComparator.Domain.ValueObjects;

namespace PriceComparator.Application.Interfaces.Repositories;

public interface ISubTypeRepository : IRepository<Subtype, long>
{ 
    Task<Subtype?> GetOrCreateAsync(long conceptId, string name, CancellationToken cancellationToken = default);
    Subtype? Extract(string name, List<Subtype> subtypes);
    Subtype ExtractByName(Product product, Concept concept, Measurement? measurement, Packaging? packaging);
}