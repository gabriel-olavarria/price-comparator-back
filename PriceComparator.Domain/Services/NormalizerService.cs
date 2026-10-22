using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PriceComparator.Domain.Services;

public static class NormalizerService
{
    public static string NormalizeText(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        normalized = Regex.Replace(normalized, @"[\u0300-\u036f]", string.Empty);
        normalized = normalized.ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"([a-z])(\d)", "$1 $2");
        normalized = Regex.Replace(normalized, @"\s+", " ");
        return normalized.Trim();
    }
    
    public static double ParseNumber(string value)
    {
        return double.Parse(value.Replace(',', '.'), CultureInfo.InvariantCulture);
    }
    
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var previousWasSpace = false;

        foreach (var character in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);

            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSpace = false;
                continue;
            }

            if (!previousWasSpace && builder.Length > 0)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    public static string[] Tokenize(string? value)
    {
        var normalized = Normalize(value);

        if (normalized.Length == 0)
        {
            return [];
        }

        return normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    public static bool ContainsTerm(string? value, string? term)
    {
        var normalizedValue = Normalize(value);
        var normalizedTerm = Normalize(term);

        if (normalizedValue.Length == 0 ||
            normalizedTerm.Length == 0)
        {
            return false;
        }

        return $" {normalizedValue} ".Contains($" {normalizedTerm} ", StringComparison.Ordinal);
    }

    public static int GetTermIndex(string? value, string? term)
    {
        var normalizedValue = Normalize(value);
        var normalizedTerm = Normalize(term);

        if (normalizedValue.Length == 0 ||
            normalizedTerm.Length == 0)
        {
            return -1;
        }

        if (!ContainsTerm(normalizedValue, normalizedTerm))
        {
            return -1;
        }

        return normalizedValue.IndexOf(normalizedTerm, StringComparison.Ordinal);
    }
    
    public static int JavaScriptRound(double value)
    {
        return (int)Math.Floor(value + 0.5);
    }
    
    public static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
    
    public static IReadOnlyCollection<string> NormalizeCategories(IReadOnlyCollection<string>? categories)
    {
        return categories?
                   .Where(category => !string.IsNullOrWhiteSpace(category))
                   .SelectMany(category => category.Split(
                       '/',
                       StringSplitOptions.RemoveEmptyEntries |
                       StringSplitOptions.TrimEntries))
                   .SelectMany(category => category.Split(
                       " y ",
                       StringSplitOptions.RemoveEmptyEntries |
                       StringSplitOptions.TrimEntries))
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .ToArray()
               ?? [];
    }
}