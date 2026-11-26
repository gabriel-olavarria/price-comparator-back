namespace PriceComparator.Application.DTOs;

public sealed class AddressSuggestionDto
{
    public string? PlaceId { get; set; }
    public string? Formatted { get; set; }
    public string? Street { get; set; }
    public string? HouseNumber { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}