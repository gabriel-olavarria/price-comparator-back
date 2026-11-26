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
    public async Task<List<CneFuelStationDto>> GetListFuels(string token, CancellationToken cancellationToken, string comuna, string region)
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

        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
     
        // Filtrar por región.
        List<JsonElement> stationsByRegion = document.RootElement
            .EnumerateArray()
            .Where(station => string.Equals(station.GetProperty("ubicacion").GetProperty("nombre_region").GetString(), region, StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        // Filtrar por comuna dentro de la región.
        List<JsonElement> stationsByCommune = stationsByRegion
            .Where(station => string.Equals(station.GetProperty("ubicacion").GetProperty("nombre_comuna").GetString(), comuna, StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        List<CneFuelStationDto> stations = stationsByCommune
            .Select(station => new CneFuelStationDto
            {
                Codigo = station.GetProperty("codigo").GetString(),
                Distribuidor = new CneDistributorDto
                {
                    Marca = station.GetProperty("distribuidor").GetProperty("marca").GetString(),
                    Logo = station.GetProperty("distribuidor").GetProperty("logo").GetString()
                },
                Ubicacion= new CneLocationDto
                {
                    NombreRegion =  station.GetProperty("ubicacion").GetProperty("nombre_region").GetString(),
                    NombreComuna =  station.GetProperty("ubicacion").GetProperty("nombre_comuna").GetString(),
                    Direccion =  station.GetProperty("ubicacion").GetProperty("direccion").GetString(), 
                    Latitud = double.Parse(station.GetProperty("ubicacion").GetProperty("latitud").GetString()!, CultureInfo.InvariantCulture),
                    Longitud = double.Parse(station.GetProperty("ubicacion").GetProperty("longitud").GetString()!, CultureInfo.InvariantCulture)
                },
                Combustibles = station.GetProperty("precios").EnumerateObject().ToDictionary(fuel => fuel.Name, fuel => new CneFuelDto
                {
                    Combustible = fuel.Name,
                    PrecioLitro = double.TryParse(fuel.Value.GetProperty("precio").GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var precio)
                        ? precio
                        : null,
                    FechaActualizacion = fuel.Value.GetProperty("fecha_actualizacion").GetString()
                })
            }).ToList();
       
        logger.LogInformation("CNE devolvió {Count} estaciones de servicio.", stations?.Count ?? 0);
       
        return stations ?? [];
    }
    
    // 3. Identificar estación más cercana al domicilio
    public CneFuelStationDto? GetNearestFuelStation(List<CneFuelStationDto> stations, double latitude, double longitude)
    {
        var nearest = stations
            .Select(station =>
            {
                var ubicacion = station.Ubicacion;
                double latitude2 = 0;
                double longitude2 = 0;
                
                if (ubicacion != null)
                {
                    latitude2 = ubicacion.Latitud!.Value;
                    longitude2 = ubicacion.Longitud!.Value;
                }

                var distance = CalculateDistanceKm(latitude, longitude, latitude2, longitude2);
                station.DistanceToUserKm = distance;
                return new
                {
                    Station = station,
                };
           
                
            })
            .OrderBy(item => item.Station.DistanceToUserKm)
            .FirstOrDefault();

        if (nearest is null)
        {
            return null;
        }

        return nearest.Station;
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
    public decimal CalculateFuelCost(double distanceKm, decimal fuelPrice, decimal fuelEfficiencyKmPerLiter)
    {
        var liters = (decimal)distanceKm / fuelEfficiencyKmPerLiter;
        var estimatedCost = liters * fuelPrice;

        return estimatedCost;
    }
    public FuelConsumptionDto CalculateFuelConsumption(double distanceKm, decimal fuelEfficiencyKmPerLiter)
    {
        var liters = (decimal)distanceKm / fuelEfficiencyKmPerLiter;

        return new FuelConsumptionDto
        {
            ConsumptionValue = liters < 1m ? liters * 1000m : liters,
            ConsumptionUnit = liters < 1m ? "ml" : "L"
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

        a = Math.Clamp(a, 0.0, 1.0);

        var c = 2 * Math.Atan2(
            Math.Sqrt(a),
            Math.Sqrt(1 - a));

        return earthRadiusKm * c;
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }
    
}