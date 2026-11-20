
namespace PriceComparator.Application.DTOs;

public sealed class GeometryDto
{
    public string? Type { get; init; }
    
    public List<double> Coordinates { get; init; } = [];
}