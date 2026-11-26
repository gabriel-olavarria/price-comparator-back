using PriceComparator.Application.DTOs;

namespace PriceComparator.Application.Interfaces.Services;

public interface IGeoapifyService
{
    Task<string> GetCommunePlaceIdAsync(double latitude, double longitude, CancellationToken cancellationToken);

    Task<IReadOnlyList<FeatureResultDto>> SearchNearbyPlacesAsync(string communePlaceId, CancellationToken cancellationToken);
    
    Task<IReadOnlyList<AddressSuggestionDto>> AutocompleteAddressAsync(string text, CancellationToken cancellationToken);
}