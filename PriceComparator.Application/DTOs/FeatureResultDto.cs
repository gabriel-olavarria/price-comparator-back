namespace PriceComparator.Application.DTOs;

public sealed class FeatureResultDto
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Brand { get; init; }
    public string? Operator { get; init; }
    public string? Formatted { get; init; }
    public string? Street { get; init; }
    public string? City { get; init; }
    public string? Country { get; init; }
    public string? CountryCode { get; init; }
    public List<string> Categories { get; init; } = [];
    public double? Distance { get; set; }
    public GeometryDto? Geometry { get; init; }
}