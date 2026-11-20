using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Persistence.Contexts;
using PriceComparator.Application.Interfaces.Repositories;

namespace PriceComparator.Infrastructure.Persistence.Repositories;


public sealed class ProductSearchRecordRepository(PriceComparatorDbContext context) 
    : Repository<ProductSearchRecord, long>(context),
        IProductSearchRecordRepository
{

}