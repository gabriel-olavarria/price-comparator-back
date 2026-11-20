
namespace PriceComparator.Application.DTOs;

public sealed class GetFuelByLocationResponse
{
    public NearestFuelStationDto? EstacionCercana { get; init; }

    public RouteDto Recorrido { get; init; } = new();

    public FuelCostDto? GastoEstimado { get; init; }
}

public sealed class FuelDistributorDto
{
    public string Marca { get; init; } = string.Empty;

    public string? Logo { get; init; }
}

public sealed class FuelLocationDto
{
    public string NombreRegion { get; init; } = string.Empty;

    public string NombreComuna { get; init; } = string.Empty;

    public string Direccion { get; init; } = string.Empty;

    public double Latitud { get; init; }

    public double Longitud { get; init; }
}

public sealed class FuelPricesDto
{
    public FuelPriceDto? Gasolina93 { get; init; }

    public FuelPriceDto? Gasolina95 { get; init; }

    public FuelPriceDto? Gasolina97 { get; init; }

    public FuelPriceDto? Diesel { get; init; }
}

public sealed class FuelPriceDto
{
    public decimal Precio { get; init; }

    public DateOnly? FechaActualizacion { get; init; }
}

