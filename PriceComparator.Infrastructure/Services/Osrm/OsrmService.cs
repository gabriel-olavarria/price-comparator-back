
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PriceComparator.Application.DTOs;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Services;

namespace PriceComparator.Infrastructure.Services.Osrm;

public sealed class OsrmService(
    IHttpClientFactory httpClientFactory,
    ILogger<OsrmService> logger
) : IOsrmService, ISingletonDependency
{
    private const string BaseUrl =
        "https://router.project-osrm.org/route/v1/driving";

    public async Task<OsrmResponseDto> GetRouteAsync(IReadOnlyList<OsrmRequestDto> points, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(points);

        // Cada solicitud representa un único tramo.
        if (points.Count != 2)
        {
            throw new ArgumentException("Se requieren exactamente dos coordenadas por tramo.", nameof(points));
        }

        // OSRM requiere longitud,latitud.
        var coordinates = string.Join(";",
            points.Select(point => string.Create(
                CultureInfo.InvariantCulture,
                $"{point.Longitude},{point.Latitude}")));

        // Configuración validada en las pruebas.
        var url = $"route/v1/driving/{coordinates}"
                  + "?approaches=curb%3Bopposite";

        logger.LogInformation(
            "Consultando ruta OSRM: {Url}",
            url);

        var client = httpClientFactory.CreateClient("OSRM");

        using var response = await client.GetAsync(url, cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var root = document.RootElement;

        // Validar respuesta de OSRM.
        var code = root.GetProperty("code").GetString();

        if (code != "Ok")
        {
            var message = root.TryGetProperty(
                "message",
                out var messageElement)
                ? messageElement.GetString()
                : "Sin detalles";

            logger.LogError(
                "OSRM devolvió código {Code}: {Message}",
                code,
                message);

            throw new InvalidOperationException(
                $"OSRM no pudo calcular el recorrido: {message}");
        }

        if (!root.TryGetProperty("routes", out var routes) ||
            routes.ValueKind != JsonValueKind.Array ||
            routes.GetArrayLength() == 0)
        {
            throw new InvalidOperationException(
                "OSRM no devolvió ningún recorrido.");
        }

        // Registrar información de cada ruta.
        foreach (var route in routes.EnumerateArray())
        {
            var distance = route
                .GetProperty("distance")
                .GetDouble();

            var duration = route
                .GetProperty("duration")
                .GetDouble();

            logger.LogInformation(
                "OSRM distancia: {DistanceKm:F4} km | " +
                "Duración: {DurationMinutes:F2} min",
                distance / 1000d,
                duration / 60d);

            var legs = route.GetProperty("legs");

            for (var i = 0; i < legs.GetArrayLength(); i++)
            {
                var leg = legs[i];

                var legDistance = leg
                    .GetProperty("distance")
                    .GetDouble();

                logger.LogInformation(
                    "OSRM tramo {Index}: {DistanceKm:F4} km",
                    i + 1,
                    legDistance / 1000d);

                if (!leg.TryGetProperty(
                    "steps",
                    out var steps))
                {
                    continue;
                }

                foreach (var step in steps.EnumerateArray())
                {
                    var road = step
                        .GetProperty("name")
                        .GetString();

                    var stepDistance = step
                        .GetProperty("distance")
                        .GetDouble();

                    var maneuver = step
                        .GetProperty("maneuver");

                    var maneuverType = maneuver
                        .GetProperty("type")
                        .GetString();

                    logger.LogInformation(
                        "Calle: {Road} | " +
                        "Distancia: {DistanceMeters:F0} m | " +
                        "Maniobra: {Maneuver}",
                        road,
                        stepDistance,
                        maneuverType);
                }
            }
        }

        // Registrar coordenadas ajustadas a la vía.
        if (root.TryGetProperty(
                "waypoints",
                out var waypoints) &&
            waypoints.ValueKind == JsonValueKind.Array)
        {
            for (var i = 0; i < waypoints.GetArrayLength(); i++)
            {
                var waypoint = waypoints[i];

                logger.LogInformation(
                    "OSRM waypoint {Index}: {Name} | " +
                    "Ajuste: {DistanceMeters:F1} m | " +
                    "Coordenadas: {Location}",
                    i,
                    waypoint.GetProperty("name").GetString(),
                    waypoint.GetProperty("distance").GetDouble(),
                    waypoint.GetProperty("location").ToString());
            }
        }

        // Convertir respuesta a DTO.
        var trips = routes
            .EnumerateArray()
            .Select(route => new OsrmTripDto
            {
                Distance = route
                    .GetProperty("distance")
                    .GetDouble(),

                Duration = route
                    .GetProperty("duration")
                    .GetDouble(),

                Legs = route
                    .GetProperty("legs")
                    .EnumerateArray()
                    .Select(leg => new OsrmLegDto
                    {
                        Distance = leg
                            .GetProperty("distance")
                            .GetDouble(),

                        Duration = leg
                            .GetProperty("duration")
                            .GetDouble()
                    })
                    .ToList()
            })
            .ToList();

        return new OsrmResponseDto
        {
            Code = code!,
            Trips = trips,
            Waypoints = []
        };
    }
}
