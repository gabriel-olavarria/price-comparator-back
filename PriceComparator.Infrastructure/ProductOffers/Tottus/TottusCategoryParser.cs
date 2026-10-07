using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PriceComparator.Infrastructure.ProductOffers.Tottus;

public sealed partial class TottusCategoryParser
{
    public IReadOnlyCollection<TottusCategory> Parse(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        var decodedHtml = WebUtility.HtmlDecode(html);

        var categories = new Dictionary<string, TottusCategory>(
            StringComparer.OrdinalIgnoreCase);

        foreach (Match parentMatch in ParentCategoryRegex().Matches(decodedHtml))
        {
            AddCategory(
                categories,
                parentMatch.Groups["name"].Value,
                parentMatch.Groups["url"].Value);

            var childrenJson =
                parentMatch.Groups["children"].Value;

            foreach (Match childMatch in CategoryRegex().Matches(childrenJson))
            {
                AddCategory(
                    categories,
                    childMatch.Groups["name"].Value,
                    childMatch.Groups["url"].Value);
            }
        }

        Console.WriteLine(
            $"[TOTTUS][CATEGORY] Categorías vigentes encontradas: {categories.Count}");

        return categories.Values.ToArray();
    }

    private static void AddCategory(
        IDictionary<string, TottusCategory> categories,
        string encodedName,
        string encodedUrl)
    {
        var name = DecodeJsonString(encodedName);
        var url = DecodeJsonString(encodedUrl);

        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        if (!IsCategoryUrl(url))
        {
            return;
        }

        categories.TryAdd(
            url,
            new TottusCategory(
                name,
                url));
    }

    private static bool IsCategoryUrl(string url)
    {
        return url.Contains(
            "/lista/CATG",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string DecodeJsonString(string value)
    {
        try
        {
            return JsonSerializer.Deserialize<string>(
                       $"\"{value}\"")
                   ?? value;
        }
        catch (JsonException)
        {
            return value;
        }
    }

    [GeneratedRegex(
        """
        "item_name"\s*:\s*"(?<name>(?:\\.|[^"\\])*)"\s*,\s*"item_url"\s*:\s*"(?<url>(?:\\.|[^"\\])*)".*?"third_level_categories"\s*:\s*\[(?<children>.*?)\]
        """,
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ParentCategoryRegex();

    [GeneratedRegex(
        """
        "item_name"\s*:\s*"(?<name>(?:\\.|[^"\\])*)"\s*,\s*"item_url"\s*:\s*"(?<url>(?:\\.|[^"\\])*)"
        """,
        RegexOptions.IgnoreCase)]
    private static partial Regex CategoryRegex();
}