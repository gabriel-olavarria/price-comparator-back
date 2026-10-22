using PriceComparator.Domain.Entities;

namespace PriceComparator.Application.Interfaces.Repositories;

public interface IConceptRepository
{
    Task<IReadOnlyCollection<Concept>> GetAllAsync(CancellationToken cancellationToken = default);
}