namespace PriceComparator.Application.DTOs;


public sealed class ConsumoVehicularResponseDto
{
    public List<VehicleBrandDto> Brands { get; init; } = [];

    public List<VehicleLabelDto> Labels { get; init; } = [];

    public List<VehicleModelDto> Models { get; init; } = [];
}

public sealed class VehicleBrandDto
{
    public int Id { get; init; }
    public string? Nombre { get; init; }
}

public sealed class VehicleLabelDto
{
    public string? Id { get; init; }
    public string? Nombre { get; init; }
}


public sealed class VehicleModelDto
{
    public int Id { get; init; }
    public string? Nombre { get; init; }
    public int MarcaId { get; init; }
}