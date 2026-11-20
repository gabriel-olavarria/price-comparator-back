using System.ComponentModel.DataAnnotations;

namespace PriceComparator.Application.DTOs;

public sealed class NearbySupermarketsRequest
{
    [Required]
    [Range(-90, 90)]
    public double Latitude { get; init; }

    [Required]
    [Range(-180, 180)]
    public double Longitude { get; init; }
    
    
    [Required]
    [MinLength(1)]
    public List<string> StoreKeys { get; init; } = [];
}