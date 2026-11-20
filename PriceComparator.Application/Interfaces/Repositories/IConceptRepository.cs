using PriceComparator.Domain.Entities;

namespace PriceComparator.Application.Interfaces.Repositories;

public interface IConceptRepository : IRepository<Concept, long>
{
    Task<IReadOnlyCollection<Concept>> GetAllConceptsAsync(CancellationToken cancellationToken = default);
}