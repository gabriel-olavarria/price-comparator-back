using PriceComparator.Application.Interfaces.ProductOffers;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Storage;

namespace PriceComparator.Infrastructure.ProductOffers.Jumbo;

public sealed class JumboProductOfferSearcher : IProductOfferSearcher
{
    private readonly HttpClient _httpClient;
    private readonly JumboProductParser _parser;
    private readonly JsonProductStore _productStore;

    public string StoreCode => "Jumbo";

    public JumboProductOfferSearcher(
        HttpClient httpClient,
        JumboProductParser parser,
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

        var normalizedQuery =
            query.Trim();

        var storedOffers =
            await _productStore.GetAsync(
                StoreCode,
                normalizedQuery,
                cancellationToken);

        if (storedOffers is not null)
        {
            Console.WriteLine(
                $"[JUMBO] Usando productos almacenados: " +
                $"{storedOffers.Count}");

            return storedOffers;
        }

        Console.WriteLine(
            "[JUMBO] No hay datos almacenados vigentes. " +
            "Consultando Jumbo.");

        var html =
            await SearchLiveAsync(
                normalizedQuery,
                cancellationToken);

        var offers =
            await _parser.ParseAsync(
                html,
                cancellationToken);

        await _productStore.SaveAsync(
            StoreCode,
            normalizedQuery,
            offers,
            cancellationToken);

        Console.WriteLine(
            $"[JUMBO] {offers.Count} productos guardados en JSON.");

        Console.WriteLine(
            $"[JUMBO] Productos encontrados: {offers.Count}");

        return offers;
    }

    private async Task<string> SearchLiveAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var encodedQuery =
            Uri.EscapeDataString(
                query);

        var requestUrl =
            $"/busqueda?ft={encodedQuery}";

        using var response =
            await _httpClient.GetAsync(
                requestUrl,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(
            cancellationToken);
    }
}