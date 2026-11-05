using Microsoft.EntityFrameworkCore;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Repositories;
using PriceComparator.Domain.Common;
using PriceComparator.Infrastructure.Persistence.Contexts;

namespace PriceComparator.Infrastructure.Persistence.Repositories;

public class Repository<TEntity, TId> : 
    IRepository<TEntity, TId>, 
    IScopedDependency where TEntity : Entity<TId>
{
    private readonly DbSet<TEntity> _entities;

    public Repository(PriceComparatorDbContext context)
    {
        _entities = context.Set<TEntity>();
    }

    public async Task<TEntity?> GetByIdAsync(
        TId id,
        CancellationToken cancellationToken = default)
    {
        return await _entities.FindAsync([id], cancellationToken);
    }

    public async Task<List<TEntity>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _entities.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        await _entities.AddAsync(entity, cancellationToken);
    }

    public void Update(TEntity entity)
    {
        _entities.Update(entity);
    }

    public void Delete(TEntity entity)
    {
        _entities.Remove(entity);
    }
}