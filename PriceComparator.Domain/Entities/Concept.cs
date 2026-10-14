using PriceComparator.Domain.ValueObjects;

namespace PriceComparator.Domain.Entities;

public sealed class Concept
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Aliases { get; set; } = [];
    public List<string> Phrases { get; set; } = [];
    
    public List<string> HeadTerms { get; set; } = [];
    public List<Subtype> Subtypes { get; set; } = [];
}