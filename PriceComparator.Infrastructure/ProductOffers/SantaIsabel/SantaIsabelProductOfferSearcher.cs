using PriceComparator.Application.Interfaces.ProductOffers;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Storage;

namespace PriceComparator.Infrastructure.ProductOffers.SantaIsabel;

public sealed class SantaIsabelProductOfferSearcher : IProductOfferSearcher
{
    private readonly HttpClient _httpClient;
    private readonly SantaIsabelProductParser _parser;
    private readonly JsonProductStore _productStore;

    public string StoreCode => "SantaIsabel";

    public SantaIsabelProductOfferSearcher(
        HttpClient httpClient,
        SantaIsabelProductParser parser,
        JsonProductStore productStore)
    {
        _httpClient = httpClient;
        _parser = parser;
        _productStore = productStore;
    }

    public async Task<IReadOnlyCollection<ProductOffer>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var normalizedQuery = query.Trim();

        var storedOffers = await _productStore.GetAsync(
            StoreCode,
            normalizedQuery,
            cancellationToken);

        if (storedOffers is not null)
        {
            Console.WriteLine(
                $"[SANTA ISABEL] Usando productos almacenados: {storedOffers.Count}");

            return storedOffers;
        }

        Console.WriteLine(
            "[SANTA ISABEL] No hay datos almacenados vigentes. Consultando Santa Isabel.");

        var html = await SearchLiveAsync(
            normalizedQuery,
            cancellationToken);

        var offers = await _parser.ParseAsync(
            html,
            cancellationToken);

        await _productStore.SaveAsync(
            StoreCode,
            normalizedQuery,
            offers,
            cancellationToken);

        Console.WriteLine(
            $"[SANTA ISABEL] {offers.Count} productos guardados en JSON.");

        Console.WriteLine(
            $"[SANTA ISABEL] Productos encontrados: {offers.Count}");

        return offers;
    }

    private async Task<string> SearchLiveAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var encodedQuery = Uri.EscapeDataString(query);

        var requestUrl =
            $"/busqueda?ft={encodedQuery}";

        Console.WriteLine(
            $"[SANTA ISABEL] Consultando: {requestUrl}");

        using var response = await _httpClient.GetAsync(
            requestUrl,
            cancellationToken);

        Console.WriteLine(
            $"[SANTA ISABEL] HTTP {(int)response.StatusCode} {response.StatusCode}");

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(
            cancellationToken);
    }
}