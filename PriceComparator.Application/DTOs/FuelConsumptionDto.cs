namespace PriceComparator.Application.DTOs;
public sealed class FuelConsumptionDto
{
    public decimal ConsumptionValue { get; init; }
    public string ConsumptionUnit { get; init; } = string.Empty;
}