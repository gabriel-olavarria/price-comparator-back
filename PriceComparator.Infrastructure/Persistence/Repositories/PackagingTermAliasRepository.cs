using PriceComparator.Application.Interfaces.Repositories;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Persistence.Contexts;

namespace PriceComparator.Infrastructure.Persistence.Repositories;

public sealed class PackagingTermAliasRepository(PriceComparatorDbContext context)
    : Repository<PackagingTermAlias, long>(context), 
        IPackagingTermAliasRepository
{

}
        
        
 