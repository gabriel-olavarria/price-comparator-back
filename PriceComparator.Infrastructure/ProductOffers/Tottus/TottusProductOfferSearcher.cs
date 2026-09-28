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
         * Los snapshots se mantienen para desarrollo,
         * pero no se escriben en Production.
         */
        if (!IsProduction())
        {
            await _snapshotStore.SaveAsync(
                StoreCode,
                query,
                html,
                cancellationToken);

            Console.WriteLine(
                $"[TOTTUS] Snapshot guardado: {query}");
        }
        else
        {
            Console.WriteLine(
                "[TOTTUS] Production - snapshot no será guardado.");
        }

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
            waitAfterLoadMs: 3000,
            keepPageOpenMs: 0,
            waitForNextData: false);
    }

    private static bool IsProduction()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable(
                "ASPNETCORE_ENVIRONMENT"),
            "Production",
            StringComparison.OrdinalIgnoreCase);
    }
}