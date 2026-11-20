using System.Text.Json.Serialization;

namespace PriceComparator.Application.DTOs;

public sealed class CneFuelStationDto
{
    [JsonPropertyName("codigo")]
    public string Codigo { get; set; } = string.Empty;

    [JsonPropertyName("en_mantenimiento")]
    public int EnMantenimiento { get; set; }

    [JsonPropertyName("distribuidor")]
    public CneDistributorDto? Distribuidor { get; set; }

    [JsonPropertyName("ubicacion")]
    public CneLocationDto? Ubicacion { get; set; }

    [JsonPropertyName("precios")]
    public Dictionary<string, CneFuelPriceDto> Precios { get; set; } = [];
}

public sealed class CneDistributorDto
{
    [JsonPropertyName("marca")]
    public string Marca { get; set; } = string.Empty;

    [JsonPropertyName("logo")]
    public string? Logo { get; set; }
}

public sealed class CneLocationDto
{
    [JsonPropertyName("nombre_region")]
    public string NombreRegion { get; set; } = string.Empty;

    [JsonPropertyName("nombre_comuna")]
    public string NombreComuna { get; set; } = string.Empty;

    [JsonPropertyName("direccion")]
    public string Direccion { get; set; } = string.Empty;

    [JsonPropertyName("latitud")]
    public string? Latitud { get; set; }

    [JsonPropertyName("longitud")]
    public string? Longitud { get; set; }
}

public sealed class CneFuelPriceDto
{
    [JsonPropertyName("precio")]
    public string? Precio { get; set; }

    [JsonPropertyName("fecha_actualizacion")]
    public string? FechaActualizacion { get; set; }
}