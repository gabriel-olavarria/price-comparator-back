
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Repositories;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Persistence.Configurations;

namespace PriceComparator.Infrastructure.ProductOffers.Acuenta;

public sealed class AcuentaProductOfferSearcher(
    HttpClient httpClient,
    AcuentaProductParser parser,
    IExtractedSearchRepository extractedSearchRepository,
    IProcessedSearchRepository processedSearchRepository,
    ILogger<AcuentaProductOfferSearcher> logger,
    IOptions<SupermarketExtractionOptions> options)
    : IProductOfferSearcher, ITransientDependency
{
    private const int MaxPages = 20;
    public string StoreCode => "Acuenta";
    private readonly SupermarketExtractionOptions _options = options.Value;

    public async Task<(IReadOnlyCollection<Product> Products, bool FromCache)> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query.Trim();
        // 1. Consultar productos procesados solamente
        // cuando Force está deshabilitado.
        if (!_options.Force)
        {
            var processedSearch = await processedSearchRepository.FindTodayAsync( StoreCode, normalizedQuery, cancellationToken);

            if (processedSearch is not null && processedSearch.Products.Count > 0)
            {
                logger.LogInformation($"[ACUENTA] Usando productos almacenados: " + $"{processedSearch.ProductCount}");
                return (processedSearch.Products, true);
            }
        }
        else
        {
            logger.LogInformation("[ACUENTA] Force habilitado. Se realizará una nueva extracción para {Query}.", normalizedQuery);
        }
        
        var allProducts = new List<Product>();

        // Conservar el contenido RSC original por página.
        var rawProducts = new List<JsonElement>();
        var uniqueProducts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var reachedEnd = false;

        // 2. Consultar páginas secuencialmente.
        for (var page = 1; page <= MaxPages; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var html = await SearchLiveAsync(normalizedQuery, page, cancellationToken);
            var products = await parser.ParseAsync(html, cancellationToken);

            if (products.Count == 0)
            {
                reachedEnd = true;
                logger.LogInformation("[ACUENTA] Página {Page} sin productos. Fin de paginación.", page);
                break;
            }

            // 3. Eliminar duplicados entre páginas.
            var added = 0;

            foreach (var product in products)
            {
                var productUrl = product.ProductUrl?.ToString();

                if (string.IsNullOrWhiteSpace(productUrl))
                {
                    logger.LogWarning("[ACUENTA] Producto sin URL descartado: {Name}", product.Name);
                    continue;
                }

                if (!uniqueProducts.Add(productUrl))
                    continue;

                allProducts.Add(product);
                added++;
            }

            // 4. Guardar datos RSC originales en vez de productos transformados.
            var rawJson = await parser.ExtractRawJsonAsync(html, cancellationToken);
            
            using (var document = JsonDocument.Parse(rawJson))
            {
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidOperationException($"aCuenta: el RAW de la página {page} no es un arreglo.");
                }

                foreach (var product in document.RootElement.EnumerateArray())
                {
                    rawProducts.Add(product.Clone());
                }
            }

            logger.LogInformation("[ACUENTA] Página {Page}: {Received} recibidos, {Added} nuevos.", page, products.Count, added);
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
           
            //Guardar el JSON original en Neon.
            await SaveExtracted(rawJson, normalizedQuery, cancellationToken);
        }

        logger.LogInformation("[ACUENTA] Extracción completada. Productos únicos: {Count}. Query: {Query}", allProducts.Count, normalizedQuery);
        return (allProducts, false);
    }

    private async Task<string> SearchLiveAsync(string query, int page, CancellationToken cancellationToken)
    {
        var url = $"https://www.acuenta.cl/search" + $"?name={Uri.EscapeDataString(query)}" + $"&currentPage={page}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml");
        request.Headers.TryAddWithoutValidation("Accept-Language", "es-CL,es;q=0.9");

        logger.LogInformation("[ACUENTA] Consultando página {Page}: {Url}", page, url);

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(html))
        {
            throw new InvalidOperationException($"aCuenta: respuesta vacía en página {page}.");
        }

        if (!html.Contains("self.__next_f.push", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"aCuenta: no se encontraron datos RSC en página {page}.");
        }

        return html;
    }
    
    public async Task SaveExtracted(string rawJson, string normalizedQuery, CancellationToken cancellationToken)
    {
        await extractedSearchRepository.SaveExtractedAsync(StoreCode, normalizedQuery, rawJson, cancellationToken);
    }

    public async Task SaveProcessed(IReadOnlyCollection<Product> products, string query, CancellationToken cancellationToken = default)
    {
        await processedSearchRepository.SaveProcessedAsync(StoreCode, query, products, cancellationToken);
    }
}
