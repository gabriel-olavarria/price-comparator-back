using System.Text.Json;
using PriceComparator.Domain.Common;

namespace PriceComparator.Domain.Entities;
public sealed class ProcessedSearch : Entity<long>
{
    public string Store { get; set; } = string.Empty;

    public string SearchQuery { get; set; } = string.Empty;

    public IReadOnlyCollection<Product> Products { get; set; } = [];
    
    public int ProductCount { get; set; }

    public DateTime GeneratedAt { get; set; }
}