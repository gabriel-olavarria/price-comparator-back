
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Primp;
using PriceComparator.Application.DTOs.Extractions;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Repositories;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Persistence.Configurations;

namespace PriceComparator.Infrastructure.ProductOffers.Unimarc;

public sealed class UnimarcProductOfferSearcher
    : IProductOfferSearcher, ITransientDependency
{
    private const string SearchEndpoint =
        "https://bff-unimarc-ecommerce.unimarc.cl/catalog/product/search";

    private readonly UnimarcProductJsonParser _parser;
    private readonly IExtractedSearcherRepository _extractedSearcherRepository;
    private readonly IProcessedSearchRepository _processedSearchRepository;
    private readonly ILogger<UnimarcProductOfferSearcher> _logger;
    private readonly SupermarketExtractionOptions _options;

    public string StoreCode => "Unimarc";

    public UnimarcProductOfferSearcher(
        UnimarcProductJsonParser parser,
        IExtractedSearcherRepository extractedSearcherRepository,
        IProcessedSearchRepository processedSearchRepository,
        ILogger<UnimarcProductOfferSearcher> logger,
        IOptions<SupermarketExtractionOptions> options)
    {
        _parser = parser;
        _extractedSearcherRepository = extractedSearcherRepository;
        _processedSearchRepository = processedSearchRepository;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<(IReadOnlyCollection<Product> Products, bool FromCache)>
        SearchAsync(
            string query,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var normalizedQuery = query.Trim();

        // 1. Consultar productos procesados solamente
        // cuando Force está deshabilitado.
        if (!_options.Force)
        {
            var processedSearch =
                await _processedSearchRepository.FindTodayAsync(
                    StoreCode,
                    normalizedQuery,
                    cancellationToken);

            if (processedSearch is { Products.Count: > 0 })
            {
                _logger.LogInformation(
                    "[UNIMARC] Usando {Count} productos almacenados.",
                    processedSearch.Products.Count);

                return (processedSearch.Products, true);
            }
        }
        else
        {
            _logger.LogInformation(
                "[UNIMARC] Force habilitado. Se realizará una nueva extracción para {Query}.",
                normalizedQuery);
        }

        // 2. Consultar productos nuevos mediante HTTP.
        var offers = await SearchJsonAsync(
            normalizedQuery,
            cancellationToken);

        return (offers, false);
    }

    private async Task<IReadOnlyCollection<Product>> SearchJsonAsync(
        string query,
        CancellationToken cancellationToken)
    {
        // Se conserva la búsqueda original:
        // page = 1, pageSize = 40.
        const int page = 1;
        const int pageSize = 40;

        var from = (page - 1) * pageSize;
        var to = from + pageSize - 1;

        var payload = new
        {
            from = from.ToString(),
            to = to.ToString(),
            searching = query.Trim(),
            promotionsOnly = false,
            orderBy = "",
            userTriggered = true
        };

        var jsonPayload = JsonSerializer.Serialize(payload);

        // Encabezados utilizados en la prueba exitosa.
        var headers = new Dictionary<string, string>
        {
            ["Accept"] = "application/json",
            ["channel"] = "UNIMARC",
            ["source"] = "web",
            ["version"] = "1.0.0"
        };

        cancellationToken.ThrowIfCancellationRequested();

        // Cliente HTTP utilizado en la prueba que obtuvo
        // HTTP 200 y 40 productos desde Unimarc.
        using var client = PrimpClient.Builder()
            .WithImpersonate(Impersonate.Chrome146)
            .WithOS(ImpersonateOS.MacOS)
            .WithTimeout(TimeSpan.FromSeconds(20))
            .WithDefaultHeaders(headers)
            .FollowRedirects(false)
            .Build();

        _logger.LogInformation(
            "[UNIMARC] Consultando {Endpoint}. Query: {Query}",
            SearchEndpoint,
            query);

        _logger.LogInformation(
            "[UNIMARC] Payload: {Payload}",
            jsonPayload);

        using var response = await client.PostAsync(
            SearchEndpoint,
            jsonPayload,
            "application/json");

        cancellationToken.ThrowIfCancellationRequested();

        var json = response.ReadAsString();
        var statusCode = (int)response.StatusCode;

        _logger.LogInformation(
            "[UNIMARC] Estado HTTP: {StatusCode}",
            statusCode);

        if (statusCode is < 200 or >= 300)
        {
            _logger.LogWarning(
                "[UNIMARC] HTTP {StatusCode}. Respuesta: {Body}",
                statusCode,
                json[..Math.Min(json.Length, 500)]);

            throw new HttpRequestException(
                $"Unimarc respondió HTTP {statusCode}.",
                null,
                (HttpStatusCode)statusCode);
        }

        cancellationToken.ThrowIfCancellationRequested();

        // 3. Guardar el JSON original en Neon.
        await SaveExtracted(
            json,
            query,
            cancellationToken);

        // 4. Transformar la respuesta al modelo Product.
        var products = _parser.Parse(json);

        _logger.LogInformation(
            "[UNIMARC] {Count} productos obtenidos. Query: {Query}",
            products.Count,
            query);

        return products;
    }

    private async Task SaveExtracted(
        string json,
        string normalizedQuery,
        CancellationToken cancellationToken)
    {
        var rawJson = _parser.ExtractRawJson(json);

        using var document = JsonDocument.Parse(rawJson);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException(
                "El JSON RAW de Unimarc debe ser un arreglo.");
        }

        var productCount = document.RootElement.GetArrayLength();

        if (productCount == 0)
        {
            _logger.LogInformation(
                "[UNIMARC] No se encontraron productos RAW para {Query}.",
                normalizedQuery);

            return;
        }

        var data = new RawExtractionDto(
            Store: StoreCode,
            Query: normalizedQuery,
            RawJson: rawJson,
            ProductCount: productCount,
            ExtractedAt: DateTimeOffset.UtcNow);

        await _extractedSearcherRepository.SaveAsync(
            data,
            cancellationToken);

        _logger.LogInformation(
            "[UNIMARC] {Count} productos RAW guardados en Neon.",
            productCount);
    }

    public async Task SaveProcessed(
        IReadOnlyCollection<Product> products,
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(products);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var normalizedQuery = query.Trim();

        var data = new ProcessedTodayDto(
            Store: StoreCode,
            Query: normalizedQuery,
            ProductCount: products.Count,
            ProcessedAt: DateTime.UtcNow,
            Products: products);

        await _processedSearchRepository.SaveAsync(
            data,
            cancellationToken);

        _logger.LogInformation(
            "[UNIMARC] {Count} productos procesados guardados en PostgreSQL. Query: {Query}",
            products.Count,
            normalizedQuery);
    }
}
