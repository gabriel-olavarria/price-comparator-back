using PriceComparator.Domain.ValueObjects;

namespace PriceComparator.Domain.Services;

public static class ProductExtractSubType
{
    public static string? Extract(string name, List<Subtype> subtypes)
    {
        var normalizedName = ProductNormalizer.NormalizeText(name);

        foreach (var subtype in subtypes)
        {
            if (ContainsAny(normalizedName, subtype.Terms))
            {
                return subtype.Id;
            }
        }

        return null;
    }

    private static bool ContainsAny(string name, IEnumerable<string> terms)
    {
        return terms.Any(term => name.Contains(ProductNormalizer.NormalizeText(term), StringComparison.Ordinal));
    }
}