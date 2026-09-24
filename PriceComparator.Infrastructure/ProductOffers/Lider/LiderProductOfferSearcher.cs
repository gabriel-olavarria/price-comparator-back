using PriceComparator.Application.Interfaces.ProductOffers;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Browsers;
using PriceComparator.Infrastructure.Snapshots;

namespace PriceComparator.Infrastructure.ProductOffers.Lider;

public sealed class LiderProductOfferSearcher : IProductOfferSearcher
{
    private readonly PlaywrightHtmlBrowser _browser;
    private readonly ISnapshotStore _snapshotStore;
    private readonly LiderProductParser _parser;

    public string StoreCode => "Lider";

    public LiderProductOfferSearcher(
        PlaywrightHtmlBrowser browser,
        ISnapshotStore snapshotStore,
        LiderProductParser parser)
    {
        _browser = browser;
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
            $"[LIDER] Snapshot guardado: {query}");

        var offers = await _parser.ParseAsync(
            html,
            cancellationToken);

        Console.WriteLine(
            "[LIDER][SUCCESS] Obtención de datos correctamente desde Lider");

        Console.WriteLine(
            $"[LIDER] Productos encontrados: {offers.Count}");

        return offers;
    }

    private async Task<string> SearchLiveAsync( string query, CancellationToken cancellationToken)
    {
        var encodedQuery = Uri.EscapeDataString(query.Trim());

        var url = $"https://super.lider.cl/search?q={encodedQuery}";

        Console.WriteLine($"[LIDER] Consultando tienda: {url}");

        return await _browser.GetHtmlAsync( url, cancellationToken, waitAfterLoadMs: 0, keepPageOpenMs: 15000);
    }
}