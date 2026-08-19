using Microsoft.Extensions.Hosting;
using PriceComparator.Application.Interfaces.ProductOffers;
using PriceComparator.Domain.Entities;
using PriceComparator.Infrastructure.Snapshots;

namespace PriceComparator.Infrastructure.ProductOffers.Jumbo;

public sealed class JumboProductOfferSearcher
    : IProductOfferSearcher
{
    private readonly HttpClient
        _httpClient;

    private readonly ISnapshotStore
        _snapshotStore;

    private readonly JumboProductParser
        _parser;

    private readonly IHostEnvironment
        _environment;

    public string StoreCode => "Jumbo";

    public JumboProductOfferSearcher(
        HttpClient httpClient,
        ISnapshotStore snapshotStore,
        JumboProductParser parser,
        IHostEnvironment environment)
    {
        _httpClient =
            httpClient;

        _snapshotStore =
            snapshotStore;

        _parser =
            parser;

        _environment =
            environment;
    }

    public async Task<
        IReadOnlyCollection<ProductOffer>>
        SearchAsync(
            string query,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(
                query))
        {
            return [];
        }

        string? html;

        if (_environment.IsDevelopment())
        {
            html =
                await SearchLiveAsync(
                    query,
                    cancellationToken);

            await _snapshotStore.SaveAsync(
                StoreCode,
                query,
                html,
                cancellationToken);

            Console.WriteLine(
                $"[JUMBO] Snapshot actualizado: {query}");
        }
        else
        {
            html =
                await _snapshotStore.GetAsync(
                    StoreCode,
                    query,
                    cancellationToken);

            if (string.IsNullOrWhiteSpace(
                    html))
            {
                Console.WriteLine(
                    $"[JUMBO] No existe snapshot para: {query}");

                return [];
            }

            Console.WriteLine(
                $"[JUMBO] Usando snapshot: {query}");
        }

        var offers =
            await _parser.ParseAsync(
                html,
                cancellationToken);

        Console.WriteLine(
            $"[JUMBO] Productos encontrados: {offers.Count}");

        return offers;
    }

    private async Task<string>
        SearchLiveAsync(
            string query,
            CancellationToken cancellationToken)
    {
        var encodedQuery =
            Uri.EscapeDataString(
                query.Trim());

        var requestUrl =
            $"/busqueda?ft={encodedQuery}";

        using var response =
            await _httpClient.GetAsync(
                requestUrl,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response
            .Content
            .ReadAsStringAsync(
                cancellationToken);
    }
}