using Microsoft.Extensions.Hosting;
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
    private readonly IHostEnvironment _environment;
    public string StoreCode => "Lider";

    public LiderProductOfferSearcher(PlaywrightHtmlBrowser browser, ISnapshotStore snapshotStore, LiderProductParser parser, IHostEnvironment environment)
    {
        _browser = browser;
        _snapshotStore = snapshotStore;
        _parser = parser;
        _environment = environment;
    }

    public async Task< IReadOnlyCollection<ProductOffer>> SearchAsync( string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace( query))
        {
            return [];
        }

        string? html;

        if (_environment.IsDevelopment())
        {
            html = await SearchLiveAsync( query, cancellationToken);
            await _snapshotStore.SaveAsync( StoreCode, query, html, cancellationToken);
            Console.WriteLine($"[LIDER] Snapshot actualizado: {query}");
        }
        else
        {
            html = await _snapshotStore.GetAsync( StoreCode, query, cancellationToken);
            if (string.IsNullOrWhiteSpace(html))
            {
                Console.WriteLine($"[LIDER] No existe snapshot para: {query}");
                return [];
            }

            Console.WriteLine($"[LIDER] Usando snapshot: {query}");
        }

        var offers = await _parser.ParseAsync( html, cancellationToken);
        Console.WriteLine($"[LIDER][SUCCESS] : Obtención de datos correctamente desde Lider");
        Console.WriteLine($"[LIDER] Productos encontrados: {offers.Count}");
        Console.WriteLine($"[API-CMP-GO]: JA! chupalo Walmart.");
        return offers;
    }

    private async Task<string> SearchLiveAsync(string query, CancellationToken cancellationToken)
    {
        var encodedQuery = Uri.EscapeDataString(query);
        var url = $"https://www.lider.cl/search?q={encodedQuery}";
        Console.WriteLine($"[LIDER] Consultando tienda: {url}");
        return await _browser.GetHtmlAsync(url, cancellationToken, waitAfterLoadMs: 0, keepPageOpenMs: 10000);
    }
}