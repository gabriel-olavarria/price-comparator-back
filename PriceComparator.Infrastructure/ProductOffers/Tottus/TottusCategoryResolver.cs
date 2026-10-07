namespace PriceComparator.Infrastructure.ProductOffers.Tottus;

public sealed class TottusCategoryResolver
{
    public IReadOnlyCollection<TottusCategory> Resolve(
        IReadOnlyCollection<TottusCategory> categories,
        string query)
    {
        if (categories.Count == 0 ||
            string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var normalizedQuery = query.Trim();

        var exactMatches = categories
            .Where(category =>
                category.Name.Equals(
                    normalizedQuery,
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (exactMatches.Length > 0)
        {
            return exactMatches;
        }

        return categories
            .Where(category =>
                category.Name.Contains(
                    normalizedQuery,
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }
}