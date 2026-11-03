using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Repositories;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Persistence.Contexts;

namespace PriceComparator.Infrastructure.Persistence.Repositories;

public sealed class ConceptRepository : IConceptRepository, IScopedDependency
{
    private readonly PriceComparatorDbContext _context;
    private readonly ILogger<ConceptRepository> _logger;

    public ConceptRepository(
        PriceComparatorDbContext context,
        ILogger<ConceptRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<Concept>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var concepts = await _context.Set<Concept>()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(c => c.Category)
            .Include(c => c.Aliases)
            .Include(c => c.Subtypes)
            .ThenInclude(s => s.Terms)
            .OrderBy(c => c.Id)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Conceptos obtenidos desde Neon: {Count}",
            concepts.Count);

        return concepts;
    }
}