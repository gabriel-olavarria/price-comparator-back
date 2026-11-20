using PriceComparator.Domain.Entities;
using PriceComparator.Domain.ValueObjects;

namespace PriceComparator.Application.Interfaces.Repositories;

public interface ISubTypeRepository : IRepository<Subtype, long>
{ 
    Subtype Extract(Product product, List<Subtype> subtypes, Concept concept, Measurement? measurement, Packaging? packaging);
}