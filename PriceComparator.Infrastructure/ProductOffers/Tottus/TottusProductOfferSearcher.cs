using PriceComparator.Application.Interfaces.ProductOffers;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Browsers;
using PriceComparator.Infrastructure.Snapshots;

namespace PriceComparator.Infrastructure.ProductOffers.Tottus;

public sealed class TottusProductOfferSearcher
    : IProductOfferSearcher
{
    private readonly PlaywrightHtmlBrowser _browser;
    private readonly ISnapshotStore _snapshotStore;
    private readonly TottusProductParser _parser;

    public string StoreCode => "Tottus";

    public TottusProductOfferSearcher(
        PlaywrightHtmlBrowser browser,
        ISnapshotStore snapshotStore,
        TottusProductParser parser)
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

        /*
         * Si Tottus respondió con una página de protección de Cloudflare,
         * no intentamos procesarla como si fuera una página de productos.
         */
        if (IsCloudflareChallenge(html))
        {
            Console.WriteLine(
                "[TOTTUS] Challenge de Cloudflare detectado. " +
                "Se devolverá una colección vacía.");

            return [];
        }

        /*
         * FileSnapshotStore decide internamente si corresponde guardar.
         * En Production no escribirá archivos.
         */
        await _snapshotStore.SaveAsync(
            StoreCode,
            query,
            html,
            cancellationToken);

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
            Uri.EscapeDataString(query.Trim());

        var url =
            $"https://www.tottus.cl/tottus-cl/buscar?Ntt={encodedQuery}";

        Console.WriteLine(
            $"[TOTTUS] Consultando tienda con Playwright: {url}");

        return await _browser.GetHtmlAsync(
            url,
            cancellationToken,
            waitAfterLoadMs: 0,
            keepPageOpenMs: 0,
            waitForNextData: false);
    }

    private static bool IsCloudflareChallenge(
        string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return false;
        }

        var cloudflareDetected =
            html.Contains(
                "__cf_chl_",
                StringComparison.OrdinalIgnoreCase)
            ||
            html.Contains(
                "cf-chl-",
                StringComparison.OrdinalIgnoreCase)
            ||
            html.Contains(
                "challenge-platform",
                StringComparison.OrdinalIgnoreCase)
            ||
            html.Contains(
                "Just a moment",
                StringComparison.OrdinalIgnoreCase);

        if (cloudflareDetected)
        {
            Console.WriteLine(
                "[TOTTUS] Se detectaron indicadores de Cloudflare en el HTML.");
        }

        return cloudflareDetected;
    }
}