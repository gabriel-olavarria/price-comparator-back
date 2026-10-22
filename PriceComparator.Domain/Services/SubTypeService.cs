using PriceComparator.Domain.ValueObjects;

namespace PriceComparator.Domain.Services;

public static class SubTypeService
{
    public static string? Extract(string name, List<Subtype> subtypes)
    {
        foreach (var subtype in subtypes)
        {
            if (ContainsAny(name, subtype.Terms))
            {
                return subtype.Id;
            }
        }

        return null;
    }

    private static bool ContainsAny(string name, IEnumerable<string> terms)
    {
        return terms.Any(term => name.Contains(NormalizerService.NormalizeText(term), StringComparison.Ordinal));
    }
}