using PriceComparator.Application.Interfaces.Persistence;
using PriceComparator.Infrastructure.Persistence.Contexts;

namespace PriceComparator.Infrastructure.Persistence;

public sealed class UnitOfWork(
    PriceComparatorDbContext context) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return context.SaveChangesAsync(cancellationToken);
    }
}