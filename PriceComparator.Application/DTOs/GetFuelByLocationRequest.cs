using System.ComponentModel.DataAnnotations;

namespace PriceComparator.Application.DTOs;

public sealed class FuelCalculationRequest
{
    public LocationRequest UserLocation { get; init; }
     
    public RouteDto Tramo { get; init; }
    
    public decimal RendimientoMixto { get; init; }
 
    
}

public sealed record UserVehicleRequest
{
    public int MarcaId { get; init; }
    public int ModeloId { get; init; }
    public string EtiquetaId { get; init; } = string.Empty;
}

public sealed class LocationRequest
{
    [Range(-90, 90)] 
    public double Latitud { get; init; }

    [Range(-180, 180)] 
    public double Longitud { get; init; }

    [Required] 
    public required string Comuna { get; init; }
    
    [Required]
    public required string Region { get; init; }
}
