namespace PriceComparator.Domain.Entities;

public sealed class ProductOffer
{
    public string Name { get; }
    public decimal Price { get; }
    public Store Store { get; }
    public Uri ProductUrl { get; }
    public Uri? ImageUrl { get; }

    public string? Brand { get; }
    public string? SellerName { get; }
    public string? SellerType { get; }
    public IReadOnlyCollection<string> Categories { get; }
    public string? Availability { get; }
    
    public bool IsMarketplace =>
        string.Equals(
            SellerType,
            "EXTERNAL",
            StringComparison.OrdinalIgnoreCase);

    
    public ProductOffer(
        string name,
        decimal price,
        Store store,
        Uri productUrl,
        Uri? imageUrl = null,
        string? brand = null,
        string? sellerName = null,
        string? sellerType = null,
        IEnumerable<string>? categories = null,
        string? availability = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "El nombre del producto es obligatorio.",
                nameof(name));
        }

        if (price <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(price),
                "El precio debe ser mayor que cero.");
        }

        Name = name.Trim();
        Price = price;
        Store = store ?? throw new ArgumentNullException(nameof(store));
        ProductUrl = productUrl
                     ?? throw new ArgumentNullException(nameof(productUrl));
        ImageUrl = imageUrl;
        Brand = NormalizeOptionalText(brand);
        SellerName = NormalizeOptionalText(sellerName);
        SellerType = NormalizeOptionalText(sellerType);
        Availability = NormalizeOptionalText(availability);
        
        Categories = categories?
                         .Where(category => !string.IsNullOrWhiteSpace(category))
                         .Select(category => category.Trim())
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .ToArray()
                     ?? [];
    }
    
    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}