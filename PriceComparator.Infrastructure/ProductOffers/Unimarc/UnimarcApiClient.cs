using System.Net.Http.Json;

namespace PriceComparator.Infrastructure.ProductOffers.Unimarc;

public sealed class UnimarcApiClient(HttpClient httpClient)
{
    public async Task<string> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "catalog/product/search");

        request.Headers.Add("channel", "UNIMARC");
        request.Headers.Add("source", "web");
        request.Headers.Add("version", "1.0.0");

        request.Content = JsonContent.Create(new
        {
            from = "0",
            orderBy = "",
            searching = query,
            promotionsOnly = false,
            to = "49",
            userTriggered = true
        });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        Console.WriteLine($"[UNIMARC API] HTTP {(int)response.StatusCode}");

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine(json);
            response.EnsureSuccessStatusCode();
        }

        return json;
    }
}