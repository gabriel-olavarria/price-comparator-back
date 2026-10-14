using PriceComparator.Domain.Entities;

namespace PriceComparator.Application.Interfaces;

public interface IConceptRepository
{
    Task<IReadOnlyCollection<Concept>> GetAllAsync(CancellationToken cancellationToken = default);
}