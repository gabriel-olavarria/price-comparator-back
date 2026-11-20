
namespace PriceComparator.Application.DTOs;



public sealed class VehicleResponseDto
{
    public int Id { get; init; }
    public string? Nombre { get; init; }

    public int MarcaId { get; init; }
    public string? NombreMarca { get; init; }

    public int ModeloId { get; init; }
    public string? NombreModelo { get; init; }

    public string? Detalle { get; init; }
    public string? Traccion { get; init; }
    public string? Transmision { get; init; }
    public string? Combustible { get; init; }
    public string? Propulsion { get; init; }

    public decimal? Cilindrada { get; init; }
    public string? TipoVehiculo { get; init; }

    public string? CodigoInformeTecnico { get; init; }
    public DateTime? FechaHomologacion { get; init; }

    public string? Categoria { get; init; }
    public string? EmpresaHomologa { get; init; }
    public string? Norma { get; init; }

    public decimal? Co2 { get; init; }

    public decimal? RendimientoUrbano { get; init; }
    public decimal? RendimientoCarretera { get; init; }
    public decimal? RendimientoMixto { get; init; }

    public decimal? RendimientoElectrico { get; init; }
    public decimal? RendimientoPonderadoCombustible { get; init; }
    public decimal? RendimientoPonderadoElectrico { get; init; }

    public decimal? Co2Combinado { get; init; }

    public decimal? RendimientoUrbanoSinAutoPista { get; init; }
    public decimal? RendimientoUrbanoConAutoPistaUrbana { get; init; }
    public decimal? RendimientoRuralConAutopista { get; init; }
    public decimal? RendimientoAutopistaInterUrbana { get; init; }

    public decimal? Combinado { get; init; }
    public decimal? WltpRendimientoElectricoPuro { get; init; }
    public decimal? RendimientoCombinadoCombustible { get; init; }
    public decimal? RendimientoCombinadoElectrico { get; init; }

    public string? TipoConectorAc { get; init; }
    public string? TipoConectorDc { get; init; }

    public decimal? CapacidadAcumulacionEnergia { get; init; }
    public decimal? CapacidadConvertidor { get; init; }

    public DateTime? FechaModificacion { get; init; }
    public string? EtiquetaId { get; init; }
}
