using PriceComparator.Application.Interfaces.ProductOffers;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Browsers;
using PriceComparator.Infrastructure.Storage;

namespace PriceComparator.Infrastructure.ProductOffers.Lider;

public sealed class LiderProductOfferSearcher : IProductOfferSearcher
{
    private readonly PlaywrightHtmlBrowser _browser;
    private readonly LiderProductParser _parser;
    private readonly JsonProductStore _productStore;

    public string StoreCode => "Lider";

    public LiderProductOfferSearcher(
        PlaywrightHtmlBrowser browser,
        LiderProductParser parser,
        JsonProductStore productStore)
    {
        _browser = browser;
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
                $"[LIDER] Usando productos almacenados: {storedOffers.Count}");

            return storedOffers;
        }

        Console.WriteLine(
            "[LIDER] No hay datos almacenados vigentes. Consultando Lider.");

        var html = await SearchLiveAsync(
            normalizedQuery,
            cancellationToken);

        // Diagnóstico del HTML recibido desde Playwright
        Console.WriteLine(
            $"[LIDER] HTML length: {html.Length}");

        Console.WriteLine(
            $"[LIDER] Robot or human: {html.Contains("Robot or human?")}");

        Console.WriteLine(
            $"[LIDER] __NEXT_DATA__: {html.Contains("__NEXT_DATA__")}");

        Console.WriteLine(
            $"[LIDER] searchResult: {html.Contains("searchResult")}");

        var offers = await _parser.ParseAsync(
            html,
            cancellationToken);

        Console.WriteLine(
            "[LIDER][SUCCESS] Obtención de datos correctamente desde Lider");

        await _productStore.SaveAsync(
            StoreCode,
            normalizedQuery,
            offers,
            cancellationToken);

        Console.WriteLine(
            $"[LIDER] {offers.Count} productos guardados en JSON.");

        Console.WriteLine(
            $"[LIDER] Productos encontrados: {offers.Count}");

        return offers;
    }

    private async Task<string> SearchLiveAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var encodedQuery =
            Uri.EscapeDataString(query);

        var url =
            $"https://super.lider.cl/search?q={encodedQuery}";

        Console.WriteLine(
            $"[LIDER] Consultando tienda: {url}");

        return await _browser.GetHtmlAsync(
            url,
            cancellationToken,
            waitAfterLoadMs: 0,
            keepPageOpenMs: 5000);
    }
}