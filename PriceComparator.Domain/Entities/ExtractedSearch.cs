using System.Text.Json;
using PriceComparator.Domain.Common;

namespace PriceComparator.Domain.Entities;

public sealed class ExtractedSearch : Entity<long>
{
    public string Store { get; set; } = string.Empty;
    public string SearchQuery { get; set; } = string.Empty;
    public JsonDocument Products { get; set; } = null!;
    public int ProductCount { get; set; }
    public DateTime ExtractedAt { get; set; }
}
