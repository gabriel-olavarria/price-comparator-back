using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PriceComparator.Application.DTOs;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Services;

namespace PriceComparator.Infrastructure.Services.Geoapify;

public sealed class GeoapifyService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<GeoapifyService> logger
) : IGeoapifyService, ISingletonDependency
{
    private const string ClientName = "Geoapify";
    private const string PlacesEndpoint = "v2/places";
    private const int PageSize = 20;
    private const string ReverseGeocodingEndpoint = "v1/geocode/reverse";
    private const string AutocompleteEndpoint = "v1/geocode/autocomplete";

    public async Task<string> GetCommunePlaceIdAsync(double latitude, double longitude, CancellationToken cancellationToken)
    {
        var apiKey = configuration["Geoapify:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("No se configuró Geoapify:ApiKey.");
        }

        if (!double.IsFinite(latitude) || !double.IsFinite(longitude) || latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), "Las coordenadas proporcionadas no son válidas.");
        }

        var lat = latitude.ToString(CultureInfo.InvariantCulture);
        var lon = longitude.ToString(CultureInfo.InvariantCulture);

        var query = new Dictionary<string, string>
        {
            ["lat"] = lat,
            ["lon"] = lon,
            ["type"] = "city",
            ["apiKey"] = apiKey
        };

        var queryString = string.Join("&", query.Select(item =>
            $"{Uri.EscapeDataString(item.Key)}=" +
            $"{Uri.EscapeDataString(item.Value)}"));

        try
        {
            var client = httpClientFactory.CreateClient(ClientName);

            using var response = await client.GetAsync($"{ReverseGeocodingEndpoint}?{queryString}", cancellationToken);

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("features", out var features) || 
                features.ValueKind != JsonValueKind.Array || 
                features.GetArrayLength() == 0)
            {
                throw new JsonException("Geoapify no devolvió una ubicación válida.");
            }

            var properties = features[0].GetProperty("properties");
            var placeId = GetString(properties, "place_id");

            if (string.IsNullOrWhiteSpace(placeId))
            {
                throw new JsonException("Geoapify no devolvió un place_id válido.");
            }

            return placeId;
        }
        catch (OperationCanceledException)when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)when (ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            logger.LogError(ex, "No fue posible obtener la comuna desde Geoapify.");

            throw new HttpRequestException("No fue posible determinar la ubicación geográfica.", ex, HttpStatusCode.ServiceUnavailable);
        }
    }
    
    public async Task<IReadOnlyList<FeatureResultDto>> SearchNearbyPlacesAsync(string communePlaceId, CancellationToken cancellationToken)
    {
        var apiKey = configuration["Geoapify:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("No se configuró Geoapify:ApiKey.");
        }

        if (string.IsNullOrWhiteSpace(communePlaceId))
        {
            throw new ArgumentException("El identificador de la comuna es obligatorio.", nameof(communePlaceId));
        }

        var supermarkets = new List<FeatureResultDto>();

        const int pageSize = 500;
        var offset = 0;

        try
        {
            var client = httpClientFactory.CreateClient(ClientName);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var query = new Dictionary<string, string>
                {
                    ["categories"] = "commercial.supermarket",
                    ["filter"] = $"place:{communePlaceId}",
                    ["limit"] = pageSize.ToString(CultureInfo.InvariantCulture),
                    ["offset"] = offset.ToString(CultureInfo.InvariantCulture),
                    ["apiKey"] = apiKey
                };

                var queryString = string.Join("&",
                    query.Select(item =>
                        $"{Uri.EscapeDataString(item.Key)}=" +
                        $"{Uri.EscapeDataString(item.Value)}"));

                using var response = await client.GetAsync($"{PlacesEndpoint}?{queryString}", cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if (!document.RootElement.TryGetProperty(
                        "features", out var features) ||
                    features.ValueKind != JsonValueKind.Array)
                {
                    throw new JsonException("Geoapify no devolvió features válidas.");
                }

                var page = features
                    .EnumerateArray()
                    .Select(MapFeature)
                    .ToList();

                supermarkets.AddRange(page);

                if (page.Count < pageSize)
                {
                    break;
                }

                offset += pageSize;
            }

            return supermarkets;
        }
        catch (OperationCanceledException)when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)when (ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            logger.LogError(ex, "No fue posible consultar Geoapify.");
            throw new HttpRequestException("El servicio de Geoapify no está disponible.", ex, HttpStatusCode.ServiceUnavailable);
        }
    }


    private static FeatureResultDto MapFeature(JsonElement feature)
    {
        var properties = feature.GetProperty("properties");

        return new FeatureResultDto
        {
            Id = GetString(properties, "place_id"),
            Name = GetString(properties, "name"),
            Brand = GetString(properties, "brand"),
            Operator = GetString(properties, "operator"),
            Formatted = GetString(properties, "formatted"),
            Street = GetString(properties, "street"),
            City = GetString(properties, "city"),
            Country = GetString(properties, "country"),
            CountryCode = GetString(properties, "country_code"),
            Categories = GetStringList(properties, "categories"),
            Distance = GetNumber(properties, "distance"),
            Geometry = MapGeometry(feature.GetProperty("geometry"))
        };
    }

    private static GeometryDto MapGeometry(JsonElement geometry)
    {
        return new GeometryDto
        {
            Type = geometry.GetProperty("type").GetString(),
            Coordinates = geometry
                .GetProperty("coordinates")
                .EnumerateArray()
                .Select(coordinate => coordinate.GetDouble())
                .ToList()
        };
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static double? GetNumber(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.Number &&
               value.TryGetDouble(out var number) &&
               double.IsFinite(number)
            ? number
            : null;
    }

    private static List<string> GetStringList(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .ToList();
    }
    
    public async Task<IReadOnlyList<AddressSuggestionDto>> AutocompleteAddressAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var apiKey = configuration["Geoapify:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "No se configuró Geoapify:ApiKey.");
        }

        var query = new Dictionary<string, string>
        {
            ["text"] = text.Trim(),
            ["filter"] = "countrycode:cl",
            ["lang"] = "es",
            ["limit"] = "5",
            ["format"] = "json",
            ["apiKey"] = apiKey
        };

        var queryString = string.Join("&", query.Select(item =>
            $"{Uri.EscapeDataString(item.Key)}=" +
            $"{Uri.EscapeDataString(item.Value)}"));

        try
        {
            var client = httpClientFactory.CreateClient(ClientName);

            using var response = await client.GetAsync(
                $"{AutocompleteEndpoint}?{queryString}",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content
                .ReadAsStreamAsync(cancellationToken);

            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty(
                    "results", out var results) ||
                results.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException(
                    "Geoapify no devolvió resultados válidos.");
            }

            return results.EnumerateArray()
                .Select(item => new AddressSuggestionDto
                {
                    PlaceId = GetString(item, "place_id"),
                    Formatted = GetString(item, "formatted"),
                    Street = GetString(item, "street"),
                    HouseNumber = GetString(item, "housenumber"),
                    City = GetString(item, "city"),
                    State = GetString(item, "state"),
                    Country = GetString(item, "country"),
                    Latitude = GetNumber(item, "lat") ?? 0,
                    Longitude = GetNumber(item, "lon") ?? 0
                })
                .Where(item =>
                    double.IsFinite(item.Latitude) &&
                    double.IsFinite(item.Longitude) &&
                    item.Latitude is >= -90 and <= 90 &&
                    item.Longitude is >= -180 and <= 180 &&
                    (item.Latitude != 0 || item.Longitude != 0))
                .ToList();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
            when (ex is HttpRequestException or
                  JsonException or
                  OperationCanceledException)
        {
            logger.LogError(
                ex,
                "No fue posible consultar direcciones en Geoapify.");

            throw new HttpRequestException(
                "El servicio de búsqueda de direcciones no está disponible.",
                ex,
                HttpStatusCode.ServiceUnavailable);
        }
    }
}