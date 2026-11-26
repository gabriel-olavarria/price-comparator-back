using System.Text.Json.Serialization;

namespace PriceComparator.Application.DTOs;

public sealed class CneFuelStationDto
{
    public string? Codigo { get; set; } = string.Empty;
    
    public double DistanceToUserKm { get; set; }
    public CneDistributorDto? Distribuidor { get; set; }
    
    public CneLocationDto? Ubicacion { get; set; }
    
    public Dictionary<string, CneFuelDto> Combustibles { get; set; } = [];
}

public sealed class CneDistributorDto
{
    public string? Marca { get; set; } = string.Empty;
    
    public string? Logo { get; set; }
}

public sealed class CneLocationDto
{
    public string? NombreRegion { get; set; } = string.Empty;
    
    public string? NombreComuna { get; set; } = string.Empty;
    
    public string? Direccion { get; set; } = string.Empty;
    
    public double? Latitud { get; set; }
    
    public double? Longitud { get; set; }
}

public sealed class CneFuelDto
{
    public string? Combustible { get; set; }
    public double? PrecioLitro { get; set; }
    public decimal? PrecioDistancia { get; set; }
    public string? FechaActualizacion { get; set; }
}