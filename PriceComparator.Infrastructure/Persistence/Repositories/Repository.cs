using Microsoft.EntityFrameworkCore;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Repositories;
using PriceComparator.Domain.Common;
using PriceComparator.Infrastructure.Persistence.Contexts;

namespace PriceComparator.Infrastructure.Persistence.Repositories;

public class Repository<TEntity, TId> :
    IRepository<TEntity, TId>,
    IScopedDependency
    where TEntity : Entity<TId>
{
    protected readonly PriceComparatorDbContext Context;
    protected readonly DbSet<TEntity> Entities;


    public Repository(PriceComparatorDbContext context)
    {
        Context = context; 
        Entities = context.Set<TEntity>();
    }
    public async Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        return await Entities.FindAsync([id], cancellationToken);
    }

    public async Task<List<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await Entities.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await Entities.AddAsync(entity, cancellationToken);
    }

    public void Update(TEntity entity)
    {
        Entities.Update(entity);
    }

    public void Delete(TEntity entity)
    {
        Entities.Remove(entity);
    }
}