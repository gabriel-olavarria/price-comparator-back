
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PriceComparator.Application.DTOs.Extractions;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Repositories;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Domain.Entities;

namespace PriceComparator.Infrastructure.ProductOffers.Acuenta;

public sealed class AcuentaProductOfferSearcher
    : IProductOfferSearcher, ITransientDependency
{
    private const int MaxPages = 20;

    private readonly HttpClient _httpClient;
    private readonly AcuentaProductParser _parser;
    private readonly ILogger<AcuentaProductOfferSearcher> _logger;
    private readonly IExtractedSearcherRepository _extractedSearcherRepository;
    private readonly IProcessedSearchRepository _processedSearchRepository;

    public string StoreCode => "Acuenta";

    public AcuentaProductOfferSearcher(
        HttpClient httpClient,
        AcuentaProductParser parser,
        IExtractedSearcherRepository extractedSearcherRepository,
        IProcessedSearchRepository processedSearchRepository,
        ILogger<AcuentaProductOfferSearcher> logger)
    {
        _httpClient = httpClient;
        _parser = parser;
        _extractedSearcherRepository = extractedSearcherRepository;
        _processedSearchRepository = processedSearchRepository;
        _logger = logger;
    }

    public async Task<(IReadOnlyCollection<Product> Products, bool FromCache)> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var normalizedQuery = query.Trim();

        // 1. Consultar productos procesados en Neon.
        var processedSearch = await _processedSearchRepository.FindTodayAsync(
            StoreCode,
            normalizedQuery,
            cancellationToken);

        if (processedSearch is not null &&
            processedSearch.Products.Count > 0)
        {
            _logger.LogInformation(
                "[ACUENTA] Usando {Count} productos almacenados. Query: {Query}",
                processedSearch.Products.Count,
                normalizedQuery);

            return (processedSearch.Products, true);
        }

        _logger.LogInformation(
            "[ACUENTA] Sin datos vigentes. Consultando aCuenta: {Query}",
            normalizedQuery);

        var allProducts = new List<Product>();

        // Conservar el contenido RSC original por página.
        var rawProducts = new List<JsonElement>();

        var uniqueProducts = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        var reachedEnd = false;

        // 2. Consultar páginas secuencialmente.
        for (var page = 1; page <= MaxPages; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var html = await SearchLiveAsync(
                normalizedQuery,
                page,
                cancellationToken);

            var products = await _parser.ParseAsync(
                html,
                cancellationToken);

            if (products.Count == 0)
            {
                reachedEnd = true;

                _logger.LogInformation(
                    "[ACUENTA] Página {Page} sin productos. Fin de paginación.",
                    page);

                break;
            }

            // 3. Eliminar duplicados entre páginas.
            var added = 0;

            foreach (var product in products)
            {
                var productUrl = product.ProductUrl?.ToString();

                if (string.IsNullOrWhiteSpace(productUrl))
                {
                    _logger.LogWarning(
                        "[ACUENTA] Producto sin URL descartado: {Name}",
                        product.Name);

                    continue;
                }

                if (!uniqueProducts.Add(productUrl))
                    continue;

                allProducts.Add(product);
                added++;
            }

            // 4. Guardar datos RSC originales en vez de productos transformados.
            var rawJson = await _parser.ExtractRawJsonAsync(
                html,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(rawJson))
            {
                throw new InvalidOperationException(
                    $"aCuenta: no se pudo extraer RAW de la página {page}.");
            }

            using (JsonDocument.Parse(rawJson))
            {
                // Validar que el RAW sea JSON válido.
            }

            using (var document = JsonDocument.Parse(rawJson))
            {
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidOperationException(
                        $"aCuenta: el RAW de la página {page} no es un arreglo.");
                }

                foreach (var product in document.RootElement.EnumerateArray())
                {
                    rawProducts.Add(product.Clone());
                }
            }

            _logger.LogInformation(
                "[ACUENTA] Página {Page}: {Received} recibidos, {Added} nuevos.",
                page,
                products.Count,
                added);
        }

        // 5. Evitar extracciones truncadas.
        if (!reachedEnd)
        {
            throw new InvalidOperationException(
                $"aCuenta: se alcanzó el límite de {MaxPages} páginas " +
                "sin detectar el final. No se guardará una extracción incompleta.");
        }

        // 6. Guardar extracción RAW en Neon.
        if (allProducts.Count > 0)
        {
            var rawJson = JsonSerializer.Serialize(rawProducts);

            var data = new RawExtractionDto(
                Store: StoreCode,
                Query: normalizedQuery,
                RawJson: rawJson,
                ProductCount: rawProducts.Count,
                ExtractedAt: DateTimeOffset.UtcNow);

            await _extractedSearcherRepository.SaveAsync(
                data,
                cancellationToken);
            
            _logger.LogInformation("[ACUENTA] Extracción RAW guardada en PostgreSQL.");
        }

        _logger.LogInformation(
            "[ACUENTA] Extracción completada. Productos únicos: {Count}. Query: {Query}",
            allProducts.Count,
            normalizedQuery);
        return (allProducts, false);
    }

    private async Task<string> SearchLiveAsync(string query, int page, CancellationToken cancellationToken)
    {
        var url =
            $"https://www.acuenta.cl/search" +
            $"?name={Uri.EscapeDataString(query)}" +
            $"&currentPage={page}";

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            url);

        request.Headers.TryAddWithoutValidation(
            "Accept",
            "text/html,application/xhtml+xml");

        request.Headers.TryAddWithoutValidation(
            "Accept-Language",
            "es-CL,es;q=0.9");

        _logger.LogInformation(
            "[ACUENTA] Consultando página {Page}: {Url}",
            page,
            url);

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(
            cancellationToken);

        if (string.IsNullOrWhiteSpace(html))
        {
            throw new InvalidOperationException(
                $"aCuenta: respuesta vacía en página {page}.");
        }

        if (!html.Contains(
                "self.__next_f.push",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"aCuenta: no se encontraron datos RSC en página {page}.");
        }

        return html;
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
            "[ACUENTA] {Count} productos procesados guardados en PostgreSQL. Query: {Query}",
            products.Count,
            normalizedQuery);
    }
}
