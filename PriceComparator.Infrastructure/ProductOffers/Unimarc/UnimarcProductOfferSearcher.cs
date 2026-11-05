
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Primp;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Persistence;
using PriceComparator.Application.Interfaces.Repositories;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Persistence.Configurations;

namespace PriceComparator.Infrastructure.ProductOffers.Unimarc;

public sealed class UnimarcProductOfferSearcher(
    UnimarcProductJsonParser parser,
    IExtractedSearcherRepository extractedSearcherRepository,
    IProcessedSearchRepository processedSearchRepository,
    ILogger<UnimarcProductOfferSearcher> logger,
    IOptions<SupermarketExtractionOptions> options)
    : IProductOfferSearcher, ITransientDependency
{
    private const string SearchEndpoint = "https://bff-unimarc-ecommerce.unimarc.cl/catalog/product/search";
    private readonly SupermarketExtractionOptions _options = options.Value;
    public string StoreCode => "Unimarc";

    public async Task<(IReadOnlyCollection<Product> Products, bool FromCache)> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query.Trim();

        // 1. Consultar productos procesados solamente
        // cuando Force está deshabilitado.
        if (!_options.Force)
        {
            var processedSearch = await processedSearchRepository.FindTodayAsync(StoreCode, normalizedQuery, cancellationToken);

            if (processedSearch is { Products.Count: > 0 })
            {
                logger.LogInformation("[UNIMARC] Usando {Count} productos almacenados.", processedSearch.Products.Count);
                return (processedSearch.Products, true);
            }
        }
        else
        {
            logger.LogInformation("[UNIMARC] Force habilitado. Se realizará una nueva extracción para {Query}.", normalizedQuery);
        }

        // 2. Consultar productos nuevos mediante HTTP.
        var offers = await SearchJsonAsync(normalizedQuery, cancellationToken);

        return (offers, false);
    }

    private async Task<IReadOnlyCollection<Product>> SearchJsonAsync(string query, CancellationToken cancellationToken)
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

        logger.LogInformation("[UNIMARC] Consultando {Endpoint}. Query: {Query}", SearchEndpoint, query);
        logger.LogInformation("[UNIMARC] Payload: {Payload}", jsonPayload);

        using var response = await client.PostAsync(SearchEndpoint, jsonPayload, "application/json");
        cancellationToken.ThrowIfCancellationRequested();
        var json = response.ReadAsString();
        var statusCode = (int)response.StatusCode;
        logger.LogInformation("[UNIMARC] Estado HTTP: {StatusCode}", statusCode);

        if (statusCode is < 200 or >= 300)
        {
            logger.LogWarning("[UNIMARC] HTTP {StatusCode}.", statusCode);
            throw new HttpRequestException($"Unimarc respondió HTTP {statusCode}.", null, (HttpStatusCode)statusCode);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var rawJson = parser.ExtractRawJson(json);
        // 3. Guardar el JSON original en Neon.
        await SaveExtracted(rawJson, query, cancellationToken);

        // 4. Transformar la respuesta al modelo Product.
        var products = parser.Parse(json);

        logger.LogInformation("[UNIMARC] {Count} productos obtenidos. Query: {Query}", products.Count, query);

        return products;
    }

    public async Task SaveExtracted(string rawJson, string normalizedQuery, CancellationToken cancellationToken)
    {
        await extractedSearcherRepository.SaveExtractedAsync(StoreCode, normalizedQuery, rawJson, cancellationToken);
    }

    public async Task SaveProcessed(IReadOnlyCollection<Product> products, string query, CancellationToken cancellationToken = default)
    {
        await processedSearchRepository.SaveProcessedAsync(StoreCode, query, products, cancellationToken);
    }
}
