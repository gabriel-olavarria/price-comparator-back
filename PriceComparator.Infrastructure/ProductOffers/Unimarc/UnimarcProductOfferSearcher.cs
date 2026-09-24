using PriceComparator.Application.Interfaces.ProductOffers;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Browsers;
using PriceComparator.Infrastructure.Snapshots;

namespace PriceComparator.Infrastructure.ProductOffers.Unimarc;

public sealed class UnimarcProductOfferSearcher
    : IProductOfferSearcher
{
    private readonly PlaywrightHtmlBrowser _browser;
    private readonly ISnapshotStore _snapshotStore;
    private readonly UnimarcProductParser _parser;

    public string StoreCode => "Unimarc";

    public UnimarcProductOfferSearcher(
        PlaywrightHtmlBrowser browser,
        ISnapshotStore snapshotStore,
        UnimarcProductParser parser)
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
            $"[UNIMARC] Snapshot guardado: {query}");

        var offers = await _parser.ParseAsync(
            html,
            cancellationToken);

        Console.WriteLine(
            $"[UNIMARC] Productos encontrados: {offers.Count}");

        return offers;
    }

    private async Task<string> SearchLiveAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var normalizedQuery =
            query.Trim().Replace(" ", "-");

        var encodedQuery =
            Uri.EscapeDataString(
                normalizedQuery);

        var url =
            $"https://www.unimarc.cl/search?q={encodedQuery}";

        Console.WriteLine(
            $"[UNIMARC] Consultando tienda: {url}");

        return await _browser.GetHtmlAsync(
            url,
            cancellationToken,
            waitAfterLoadMs: 5000,
            keepPageOpenMs: 0);
    }
}