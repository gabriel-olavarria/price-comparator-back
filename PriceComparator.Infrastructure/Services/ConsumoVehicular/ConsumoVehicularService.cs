using System.Text.Json;
using PriceComparator.Application.DTOs;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Services;

namespace PriceComparator.Infrastructure.Services.ConsumoVehicular;

public sealed class ConsumoVehicularService(
    IHttpClientFactory httpClientFactory
) : IConsumoVehicularService, ISingletonDependency
{
    public async Task<ConsumoVehicularResponseDto> GetCatalogAsync(CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("ConsumoVehicular");

        // 1. Obtener marcas
        using var brandsResponse = await client.GetAsync("marcas?SoloCicloWLTP=false", cancellationToken);

        brandsResponse.EnsureSuccessStatusCode();
        await using var brandsStream = await brandsResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var brandsDocument = await JsonDocument.ParseAsync(brandsStream, cancellationToken: cancellationToken);

        var brands = brandsDocument.RootElement
            .EnumerateArray()
            .Select(brand => new VehicleBrandDto
            {
                Id = brand.GetProperty("id").GetInt32(),
                Nombre = brand.GetProperty("nombre").GetString()
            })
            .ToList();

        // 2. Obtener todas las etiquetas
        var labels = new List<VehicleLabelDto>();
        var pageIndex = 1;
        var totalPages = 1;

        do
        {
            using var response = await client.GetAsync($"vehiculos/etiquetas?PageIndex={pageIndex}&PageSize=100", cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            totalPages = root.GetProperty("totalPages").GetInt32();

            labels.AddRange(root.GetProperty("items")
                .EnumerateArray()
                .Select(label => new VehicleLabelDto
                {
                    Id = label.GetProperty("id").GetString(),
                    Nombre = label.GetProperty("nombre").GetString()
                }));

            pageIndex++;
        } while (pageIndex <= totalPages);

        // 3. Obtener todos los modelos
        var models = new List<VehicleModelDto>();
        pageIndex = 1;
        totalPages = 1;

        do
        {
            using var response = await client.GetAsync($"modelos?PageIndex={pageIndex}&PageSize=1000", cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            totalPages = root.GetProperty("totalPages").GetInt32();

            models.AddRange(root.GetProperty("items")
                .EnumerateArray()
                .Select(model => new VehicleModelDto
                {
                    Id = model.GetProperty("id").GetInt32(),
                    Nombre = model.GetProperty("nombre").GetString(),
                    MarcaId = model.GetProperty("marcaId").GetInt32()
                }));

            pageIndex++;
        } while (pageIndex <= totalPages);

        // 4. Consolidar catálogos
        return new ConsumoVehicularResponseDto
        {
            Brands = brands,
            Labels = labels,
            Models = models
        };
    }
    
    public async Task<VehicleResponseDto> GetVehiclesAsync(UserVehicleRequest userVehicleRequest, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("ConsumoVehicular");
        var url = "vehiculos/listar" +
                  $"?MarcaId={userVehicleRequest.MarcaId}" +
                  $"&ModeloId={userVehicleRequest.ModeloId}" +
                  $"&EtiquetaId={Uri.EscapeDataString(userVehicleRequest.EtiquetaId)}" +
                  "&PageIndex=1" +
                  "&PageSize=3" +
                  "&SoloCicloWltc=false";

        using var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        var vehicle = root.GetProperty("items")
            .EnumerateArray()
            .Select(vehicle => new VehicleResponseDto
            {
                Id = vehicle.GetProperty("id").GetInt32(),
                Nombre = vehicle.GetProperty("nombre").GetString(),
                MarcaId = vehicle.GetProperty("marcaId").GetInt32(),
                NombreMarca = vehicle.GetProperty("nombreMarca").GetString(),
                ModeloId = vehicle.GetProperty("modeloId").GetInt32(),
                NombreModelo = vehicle.GetProperty("nombreModelo").GetString(),
                Detalle = vehicle.GetProperty("detalle").GetString(),
                Traccion = vehicle.GetProperty("traccion").GetString(),
                Transmision = vehicle.GetProperty("transmision").GetString(),
                Combustible = vehicle.GetProperty("combustible").GetString(),
                Propulsion = vehicle.GetProperty("propulsion").GetString(),
                Cilindrada = GetNullableDecimal(vehicle, "cilindrada"),
                TipoVehiculo = vehicle.GetProperty("tipoVehiculo").GetString(),
                RendimientoUrbano = GetNullableDecimal(vehicle, "rendimientoUrbano"),
                RendimientoCarretera = GetNullableDecimal(vehicle, "rendimientoCarretera"),
                RendimientoMixto = GetNullableDecimal(vehicle, "rendimientoMixto"),
                RendimientoElectrico = GetNullableDecimal(vehicle, "rendimientoElectrico"),
                EtiquetaId = vehicle.GetProperty("etiquetaId").GetString()
            }).First();

        return vehicle;
    }

    private static decimal? GetNullableDecimal(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDecimal()
            : null;
    }
}