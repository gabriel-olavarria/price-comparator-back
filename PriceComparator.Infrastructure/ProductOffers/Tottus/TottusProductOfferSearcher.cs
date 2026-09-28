using PriceComparator.Application.Interfaces.ProductOffers;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Snapshots;

namespace PriceComparator.Infrastructure.ProductOffers.Tottus;

public sealed class TottusProductOfferSearcher
    : IProductOfferSearcher
{
    private readonly HttpClient _httpClient;
    private readonly ISnapshotStore _snapshotStore;
    private readonly TottusProductParser _parser;

    public string StoreCode => "Tottus";

    public TottusProductOfferSearcher(
        HttpClient httpClient,
        ISnapshotStore snapshotStore,
        TottusProductParser parser)
    {
        _httpClient = httpClient;
        _snapshotStore = snapshotStore;
        _parser = parser;
    }

    public async Task<IReadOnlyCollection<ProductOffer>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var html = await SearchLiveAsync(
            query,
            cancellationToken);

        await _snapshotStore.SaveAsync(
            StoreCode,
            query,
            html,
            cancellationToken);

        Console.WriteLine(
            $"[TOTTUS] Snapshot guardado: {query}");

        var offers = await _parser.ParseAsync(
            html,
            cancellationToken);

        Console.WriteLine(
            $"[TOTTUS] Productos encontrados: {offers.Count}");

        return offers;
    }

    private async Task<string> SearchLiveAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var encodedQuery =
            Uri.EscapeDataString(
                query.Trim());

        var requestUrl =
            $"/tottus-cl/buscar?Ntt={encodedQuery}";

        using var response =
            await _httpClient.GetAsync(
                requestUrl,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(
            cancellationToken);
    }
}