using PriceComparator.Domain.Entities;

namespace PriceComparator.Application.Interfaces.Services;

public interface IRankedService
{
    IReadOnlyCollection<Product> Rank(string query, IEnumerable<Product> products);
    
}
