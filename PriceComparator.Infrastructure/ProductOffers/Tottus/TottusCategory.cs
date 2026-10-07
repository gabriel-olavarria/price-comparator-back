namespace PriceComparator.Infrastructure.ProductOffers.Tottus;

public sealed class TottusCategory
{
    public string Name { get; }
    public string Url { get; }

    public TottusCategory(
        string name,
        string url)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "El nombre de la categoría es obligatorio.",
                nameof(name));
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException(
                "La URL de la categoría es obligatoria.",
                nameof(url));
        }

        Name = name.Trim();
        Url = url.Trim();
    }
}