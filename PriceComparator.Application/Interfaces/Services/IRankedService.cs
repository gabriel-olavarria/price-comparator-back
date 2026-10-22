using PriceComparator.Domain.Entities;
using PriceComparator.Application.Models;

namespace PriceComparator.Application.Interfaces.Services;

public interface IRankedService
{
    IReadOnlyCollection<Product> Rank(string query, IEnumerable<Product> products);
    
}
