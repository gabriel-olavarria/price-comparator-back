using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Repositories;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Persistence.Contexts;

namespace PriceComparator.Infrastructure.Persistence.Repositories;

public sealed class ConceptRepository(
    PriceComparatorDbContext context)
    :  Repository<Concept, long>(context), 
        IConceptRepository
{
    public async Task<IReadOnlyCollection<Concept>> GetAllConceptsAsync(CancellationToken cancellationToken = default)
    {
        var concepts = await Entities
            .AsSplitQuery()
            .Include(c => c.Category)
            .Include(c => c.ConceptTerms)
            .Include(c => c.Subtypes)
            .ThenInclude(s => s.Terms)
            .OrderBy(c => c.Id)
            .ToListAsync(cancellationToken);
        return concepts;
    }
}