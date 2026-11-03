
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PriceComparator.Application.Interfaces.DependencyInjection;
using PriceComparator.Application.Interfaces.Services;
using PriceComparator.Domain.Entities;

namespace PriceComparator.Application.Services;

public sealed class ProductConceptFilter : IProductConceptFilter, IScopedDependency
{
    public IReadOnlyCollection<Product> Filter(IEnumerable<Product> products, string query)
    {
        ArgumentNullException.ThrowIfNull(products);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var normalizedQuery = Normalize(query);

        return products
            .Where(product => IsNormalizedMatch(product, normalizedQuery))
            .ToArray();
    }

    public bool IsMatch(Product product, string query)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        return IsNormalizedMatch(product, Normalize(query));
    }

    private static bool IsNormalizedMatch(Product product, string normalizedQuery)
    {
        if (string.IsNullOrWhiteSpace(product.Name))
            return false;

        var normalizedName = Normalize(product.Name);

        if (normalizedName.Length == 0)
            return false;

        var queryWords = normalizedQuery.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);

        var productWords = normalizedName.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);

        var wordSet = new HashSet<string>(
            productWords,
            StringComparer.Ordinal);

        return queryWords.All(word => wordSet.Contains(word));
    }

    private static string Normalize(string value)
    {
        var decomposed = value
            .Trim()
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);

            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            builder.Append(char.IsLetterOrDigit(character)
                ? character
                : ' ');
        }

        var normalized = builder
            .ToString()
            .Normalize(NormalizationForm.FormC);

        return Regex.Replace(
            normalized,
            @"\s+",
            " ").Trim();
    }
}
