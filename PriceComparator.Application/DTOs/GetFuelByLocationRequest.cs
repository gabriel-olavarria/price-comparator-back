using System.ComponentModel.DataAnnotations;

namespace PriceComparator.Application.DTOs;

public sealed class GetFuelByLocationRequest
{
    [Required]
    public required string Direccion { get; init; }

    [Range(-90, 90)]
    public double Latitud { get; init; }

    [Range(-180, 180)]
    public double Longitud { get; init; }

    [Required]
    public required string Comuna { get; init; }

    [Required]
    public required string Region { get; init; }

    public List<FeatureResultDto> Supermercados { get; init; } = [];
    
    public string TipoCombustible { get; init; } = "93";
    
    public decimal RendimientoMixto { get; init; }
    
}

public sealed record UserVehicleRequest
{
    public int MarcaId { get; init; }
    public int ModeloId { get; init; }
    public string EtiquetaId { get; init; } = string.Empty;
}

