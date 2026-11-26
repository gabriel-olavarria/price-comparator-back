using PriceComparator.Application.DTOs;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Services;

namespace PriceComparator.Application.UseCases.NearbySupermarket.Get;

public sealed class GetNearbySupermarketsUseCase(
    IGeoapifyService geoapifyService,
    IFuelStationService  fuelStationService
) : IScopedDependency
{

    public async Task<NearbySupermarketsResponse> ExecuteAsync(NearbySupermarketsRequest request, CancellationToken cancellationToken)
    {
        // 1. Identificar la comuna.
        var communePlaceId = await geoapifyService.GetCommunePlaceIdAsync(request.Latitude, request.Longitude, cancellationToken);

        // 2. Obtener los supermercados de la comuna.
        var supermarkets = await geoapifyService.SearchNearbyPlacesAsync(communePlaceId, cancellationToken);

        // 3. Identificar las cadenas del carrito.
        var selectedStores = request.StoreKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 4. Identificar cadena y calcular distancia.
        var candidates = supermarkets
            .Select(supermarket =>
            {
                var storeKey = selectedStores.FirstOrDefault(key =>
                    MatchesStore(supermarket.Brand, key) ||
                    MatchesStore(supermarket.Name, key));

                var coordinates = supermarket.Geometry?.Coordinates;

                if (storeKey is null || coordinates is null || coordinates.Count < 2)
                {
                    return null;
                }

                var longitude = coordinates[0];
                var latitude = coordinates[1];

                if (!double.IsFinite(latitude) || !double.IsFinite(longitude) || latitude is < -90 or > 90 || longitude is < -180 or > 180)
                {
                    return null;
                }

                var distance = CalculateDistanceMeters(request.Latitude, request.Longitude, latitude, longitude);
                supermarket.Distance = distance;

                return new
                {
                    StoreKey = storeKey,
                    Supermarket = supermarket,
                    Distance = distance
                };
            })
            .Where(item => item is not null)
            .ToList();

        // 5. Seleccionar la sucursal más cercana por cadena.
        var nearestByStore = candidates
            .GroupBy(item => item!.StoreKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.MinBy(item => item!.Distance)!.Supermarket)
            .OrderBy(item => item.Distance)
            .ToArray();
        
        
        // 6. Calcular el recorrido por carretera.
        var route = await fuelStationService.CalculateRouteAsync(
            request.Latitude,
            request.Longitude,
            nearestByStore,
            cancellationToken);
        

        return new NearbySupermarketsResponse
        {
            NearestByStore = nearestByStore, 
            RouteResult = route
        };
    }
    private static bool MatchesStore(string? value, string storeKey)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            string.IsNullOrWhiteSpace(storeKey))
            return false;

        static string Normalize(string text) =>
            string.Concat(text.Where(c => !char.IsWhiteSpace(c)))
                .ToLowerInvariant();

        return string.Equals(
            Normalize(value),
            Normalize(storeKey),
            StringComparison.Ordinal);
    }
    
    private static double CalculateDistanceMeters(double originLatitude, double originLongitude, double destinationLatitude, double destinationLongitude)
    {
        const double earthRadiusMeters = 6_371_000;

        static double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }

        var latitudeDifference = ToRadians(destinationLatitude - originLatitude);
        var longitudeDifference = ToRadians(destinationLongitude - originLongitude);
        var originLatRadians = ToRadians(originLatitude);
        var destinationLatRadians = ToRadians(destinationLatitude);

        var a =
            Math.Pow(Math.Sin(latitudeDifference / 2), 2) +
            Math.Cos(originLatRadians) *
            Math.Cos(destinationLatRadians) *
            Math.Pow(Math.Sin(longitudeDifference / 2), 2);

        var c = 2 * Math.Asin(Math.Sqrt(Math.Clamp(a, 0, 1)));

        return earthRadiusMeters * c;
    }


}