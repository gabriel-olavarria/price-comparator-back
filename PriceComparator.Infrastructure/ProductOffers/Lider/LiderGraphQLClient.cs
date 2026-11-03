
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PriceComparator.Infrastructure.ProductOffers.Lider;

public sealed class LiderGraphQLClient
{
    private const int PageSize = 40;
    private const string GraphQLPath = "/orchestra/graphql/search";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(45);

    private const string SearchQuery = """
        query Search(
          $query: String
          $page: Int
          $prg: Prg!
          $ps: Int
          $limit: Int
        ) {
          search(
            query: $query
            page: $page
            prg: $prg
            ps: $ps
            limit: $limit
          ) {
            query
            searchResult {
              aggregatedCount
              paginationV2 {
                maxPage
                pageProperties
              }
              itemStacks {
                itemsV2 {
                  __typename
                  ... on Product {
                    name
                    brand
                    canonicalUrl
                    sellerName
                    category {
                      path {
                        name
                      }
                    }
                    imageInfo {
                      thumbnailUrl
                    }
                    priceInfo {
                      currentPrice {
                        price
                        priceString
                      }
                      unitPrice {
                        priceString
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """;

    private readonly HttpClient _httpClient;
    private readonly ILogger<LiderGraphQLClient> _logger;

    public LiderGraphQLClient(HttpClient httpClient, ILogger<LiderGraphQLClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LiderGraphQLHttpResponse> SearchAsync(string query, int page = 1, CancellationToken cancellationToken = default)
    {

        var normalizedQuery = query.Trim();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(RequestTimeout);
        var token = timeoutCts.Token;
       
        var payload = new
        {
            query = SearchQuery,
            variables = new
            {
                query = normalizedQuery,
                page,
                prg = "mWeb",
                ps = PageSize,
                limit = PageSize
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, GraphQLPath)
        {
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            Content = JsonContent.Create(payload)
        };

        ConfigureHeaders(request);

        _logger.LogInformation(
            "[LIDER] Consultando GraphQL. Query: {Query}, Página: {Page}",
            normalizedQuery,
            page);

        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);

            var body = await response.Content.ReadAsStringAsync(token);

            var result = new LiderGraphQLHttpResponse(
                StatusCode: (int)response.StatusCode,
                HttpVersion: response.Version.ToString(),
                FinalUrl: response.RequestMessage?.RequestUri?.ToString() ?? string.Empty,
                ContentType: response.Content.Headers.ContentType?.MediaType,
                RedirectLocation: response.Headers.Location?.ToString(),
                Body: body
            );

            ValidateResponse(result, normalizedQuery, page);

            _logger.LogInformation(
                "[LIDER] Respuesta correcta. Query: {Query}, Página: {Page}, Bytes: {Length}",
                normalizedQuery,
                page,
                body.Length);

            return result;
        }
        catch (OperationCanceledException exception)when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                exception,
                "[LIDER] Tiempo de espera agotado. Query: {Query}, Página: {Page}",
                normalizedQuery,
                page);

            throw new TimeoutException(
                $"[LIDER] La consulta GraphQL superó " +
                $"{RequestTimeout.TotalSeconds} segundos. " +
                $"Query: {normalizedQuery}, página: {page}.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(
                exception,
                "[LIDER] Error HTTP. Query: {Query}, Página: {Page}",
                normalizedQuery,
                page);

            throw;
        }
    }

    private static void ConfigureHeaders(HttpRequestMessage request)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("x-apollo-operation-name", "Search");
        request.Headers.TryAddWithoutValidation("x-o-bu", "LIDER-CL");
        request.Headers.TryAddWithoutValidation("x-o-mart", "B2C");
        request.Headers.TryAddWithoutValidation("x-o-vertical", "OD");
    }

    private static void ValidateResponse(LiderGraphQLHttpResponse response, string query, int page)
    {
        if (response.StatusCode != (int)HttpStatusCode.OK)
        {
            throw new HttpRequestException(
                $"[LIDER] GraphQL respondió HTTP " +
                $"{response.StatusCode}. " +
                $"Query: {query}, página: {page}. " +
                $"URL: {response.FinalUrl}",
                null,
                (HttpStatusCode)response.StatusCode);
        }

        if (response.ContentType is null || !response.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"[LIDER] La respuesta no es JSON. " +
                $"Content-Type: {response.ContentType}. " +
                $"Query: {query}, página: {page}.");
        }

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(response.Body);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"[LIDER] GraphQL devolvió JSON inválido. " +
                $"Query: {query}, página: {page}.",
                exception);
        }

        using (document)
        {
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    "[LIDER] La respuesta GraphQL no es un objeto JSON.");
            }

            if (root.TryGetProperty("errors", out var errors) 
                && errors.ValueKind == JsonValueKind.Array 
                && errors.GetArrayLength() > 0)
            {
                throw new InvalidOperationException(
                    $"[LIDER] GraphQL devolvió errores. " +
                    $"Query: {query}, página: {page}. " +
                    $"Detalle: {errors.GetRawText()}");
            }

            if (!root.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Object ||
                !data.TryGetProperty("search", out var search) ||
                search.ValueKind != JsonValueKind.Object ||
                !search.TryGetProperty("searchResult", out var searchResult) ||
                searchResult.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    $"[LIDER] La respuesta no contiene " +
                    $"data.search.searchResult. " +
                    $"Query: {query}, página: {page}.");
            }
        }
    }
}

public sealed record LiderGraphQLHttpResponse(
    int StatusCode,
    string HttpVersion,
    string FinalUrl,
    string? ContentType,
    string? RedirectLocation,
    string Body
);
