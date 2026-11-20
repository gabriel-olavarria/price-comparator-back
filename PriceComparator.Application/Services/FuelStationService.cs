using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PriceComparator.Application.DTOs;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Services;

namespace PriceComparator.Application.Services;

public sealed class FuelStationService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<FuelStationService> logger,
    IOsrmService osrmService
) : IFuelStationService, ISingletonDependency
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // 1. Obtener listado completo de estaciones CNE
    public async Task<List<CneFuelStationDto>> GetListFuels(string token, CancellationToken cancellationToken)
    {
        var stationsUrl = configuration["Cne:StationsUrl"];

        if (string.IsNullOrWhiteSpace(stationsUrl))
        {
            throw new InvalidOperationException("No se ha configurado Cne:StationsUrl.");
        }

        var client = httpClientFactory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, stationsUrl);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var stations = await JsonSerializer.DeserializeAsync<List<CneFuelStationDto>>(stream, JsonOptions, cancellationToken);

        logger.LogInformation("CNE devolvió {Count} estaciones de servicio.", stations?.Count ?? 0);
        return stations ?? [];
    }

    // 2. Filtrar estaciones por región y comuna
    public Task<List<CneFuelStationDto>> GetListFuelsByLocation(List<CneFuelStationDto> stations, string region, string comuna, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = stations
            .Where(station =>
            {
                var ubicacion = station.Ubicacion;

                return ubicacion is not null &&
                       string.Equals(ubicacion.NombreRegion?.Trim(), region.Replace("Región", "").Replace("de", "").Trim(), StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(ubicacion.NombreComuna?.Trim(), comuna.Trim(), StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        logger.LogInformation("Se encontraron {Count} estaciones en {Comuna}, {Region}.", result.Count, comuna, region);
        return Task.FromResult(result);
    }

    // 3. Identificar estación más cercana al domicilio
    public NearestFuelStationDto? GetNearestFuelStation(List<CneFuelStationDto> stations, double latitude, double longitude)
    {
        var nearest = stations
            .Select(station =>
            {
                var ubicacion = station.Ubicacion;

                if (ubicacion is null || 
                    !double.TryParse(ubicacion.Latitud, NumberStyles.Float, CultureInfo.InvariantCulture, out var stationLatitude) ||
                    !double.TryParse(ubicacion.Longitud, NumberStyles.Float, CultureInfo.InvariantCulture, out var stationLongitude))
                {
                    return new
                    {
                        Station = station,
                        DistanceKm = (double?)null
                    };
                }

                var distance = CalculateDistanceKm(latitude, longitude, stationLatitude, stationLongitude);

                return new
                {
                    Station = station,
                    DistanceKm = (double?)distance
                };
            })
            .Where(item => item.DistanceKm.HasValue)
            .OrderBy(item => item.DistanceKm)
            .FirstOrDefault();

        // Pattern matching: valida el objeto y extrae el double.
        if (nearest is not { DistanceKm: double distanceKm })
        {
            return null;
        }

        return new NearestFuelStationDto
        {
            Station = nearest.Station,
            DistanceKm = Math.Round(distanceKm, 2),
            Precios = MapFuelPrices(nearest.Station.Precios)
        };
    }


    public async Task<RouteDto> CalculateRouteAsync(double homeLatitude, double homeLongitude, IReadOnlyList<FeatureResultDto> supermarkets, CancellationToken cancellationToken)
    {
        var validSupermarkets = supermarkets
            .Where(s => s.Geometry?.Coordinates is { Count: >= 2 })
            .ToList();

        if (validSupermarkets.Count == 0)
        {
            return new RouteDto
            {
                TotalDistanceKm = 0,
                Segments = []
            };
        }

        // 1. Casa como punto de partida
        var points = new List<OsrmRequestDto>
        {
            new()
            {
                Latitude = homeLatitude,
                Longitude = homeLongitude
            }
        };

        // 2. Agregar supermercados en el orden recibido
        points.AddRange(validSupermarkets.Select(supermarket =>
            new OsrmRequestDto
            {
                Latitude = supermarket.Geometry!.Coordinates[1],
                Longitude = supermarket.Geometry.Coordinates[0]
            }));

        // 3. Regresar a casa
        points.Add(new OsrmRequestDto
        {
            Latitude = homeLatitude,
            Longitude = homeLongitude
        });

        // 4. Nombres de las paradas
        var names = new List<string> { "Casa" };

        names.AddRange(validSupermarkets.Select(supermarket => supermarket.Name ?? supermarket.Brand ?? "Supermercado"));
        names.Add("Casa");

        // 5. Consultar cada tramo individualmente
        var segments = new List<RouteSegmentDto>();
        double totalDistanceMeters = 0;

        for (var i = 0; i < points.Count - 1; i++)
        {
            var segmentPoints = new List<OsrmRequestDto>
            {
                points[i],
                points[i + 1]
            };

            var response = await osrmService.GetRouteAsync(segmentPoints, cancellationToken);
            var route = response.Trips.First();
            totalDistanceMeters += route.Distance;

            segments.Add(new RouteSegmentDto
            {
                From = names[i],
                To = names[i + 1],
                DistanceKm = Math.Round(route.Distance / 1000d, 4)
            });
        }

        return new RouteDto
        {
            TotalDistanceKm = Math.Round(totalDistanceMeters / 1000d, 4),
            Segments = segments
        };
    }


    // 5. Calcular gasto estimado de combustible
    public FuelCostDto CalculateFuelCost(double distanceKm, decimal fuelPrice, decimal fuelEfficiencyKmPerLiter)
    {
        if (distanceKm < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distanceKm));
        }

        if (fuelPrice <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fuelPrice));
        }

        if (fuelEfficiencyKmPerLiter <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fuelEfficiencyKmPerLiter));
        }

        var liters = (decimal)distanceKm / fuelEfficiencyKmPerLiter;
        var estimatedCost = liters * fuelPrice;

        return new FuelCostDto
        {
            DistanceKm = distanceKm,
            FuelPrice = fuelPrice,
            FuelEfficiencyKmPerLiter = fuelEfficiencyKmPerLiter,
            EstimatedLiters = Math.Round(liters, 4),
            EstimatedCost = Math.Round(estimatedCost, 0, MidpointRounding.AwayFromZero)
        };
    }

    // 6. Convertir los precios de combustible CNE
    private static FuelPricesDto MapFuelPrices(Dictionary<string, CneFuelPriceDto>? prices)
    {
        return new FuelPricesDto
        {
            Gasolina93 = MapFuelPrice(prices, "93"),
            Gasolina95 = MapFuelPrice(prices, "95"),
            Gasolina97 = MapFuelPrice(prices, "97"),
            Diesel = MapFuelPrice(prices, "DI")
        };
    }

    private static FuelPriceDto? MapFuelPrice(Dictionary<string, CneFuelPriceDto>? prices, string fuelType)
    {
        if (prices is null || 
            !prices.TryGetValue(fuelType, out var fuel) ||
            fuel is null ||
            !decimal.TryParse(fuel.Precio, NumberStyles.Number, CultureInfo.InvariantCulture, out var price) ||
            price <= 0)
        {
            return null;
        }

        DateOnly? updatedAt = null;

        if (DateOnly.TryParseExact(fuel.FechaActualizacion, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            updatedAt = parsedDate;
        }

        return new FuelPriceDto
        {
            Precio = price,
            FechaActualizacion = updatedAt
        };
    }

    // 7. Distancia geográfica mediante Haversine
    private static double CalculateDistanceKm(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        const double earthRadiusKm = 6371.0;

        var latitudeDifference = ToRadians(latitude2 - latitude1);
        var longitudeDifference = ToRadians(longitude2 - longitude1);

        var a =
            Math.Pow(Math.Sin(latitudeDifference / 2), 2) +
            Math.Cos(ToRadians(latitude1)) *
            Math.Cos(ToRadians(latitude2)) *
            Math.Pow(Math.Sin(longitudeDifference / 2), 2);

        var c = 2 * Math.Atan2(
            Math.Sqrt(a),
            Math.Sqrt(Math.Max(0, 1 - a)));

        return earthRadiusKm * c;
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }
}