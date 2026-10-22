using System.Text.Json.Serialization;
using PriceComparator.Domain.Services;
using PriceComparator.Domain.ValueObjects;

namespace PriceComparator.Domain.Entities;

public sealed class Product
{
    public string Name { get; }
    public decimal Price { get; }
    public Store Store { get; set; }
    public Uri ProductUrl { get; }
    public Uri? ImageUrl { get; }
    public string? Brand { get; }
    public string? SellerName { get; }
    [JsonPropertyName("categories")]
    public IReadOnlyCollection<string> CategoriesStore { get; }
    public string? Availability { get; }
    public string? CanonicalKey { get; set; }
    public double Score { get; set; } = 0;
    public Normalized Normalized { get; set; }
    public Clasification? Clasification { get; set; }

    public Product(string name, decimal price, Store store, Uri productUrl, Uri? imageUrl = null, string? brand = null, string? sellerName = null, IReadOnlyCollection<string>? categoriesStore  = null, string? availability = null) {
       
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El nombre del producto es obligatorio.", nameof(name));
        }

        if (price <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "El precio debe ser mayor que cero.");
        }

        Name = name.Trim();
        Price = price;
        Store = store ?? throw new ArgumentNullException(nameof(store));
        ProductUrl = productUrl ?? throw new ArgumentNullException(nameof(productUrl));
        ImageUrl = imageUrl;
        Brand = NormalizerService.NormalizeOptionalText(brand);
        SellerName = NormalizerService.NormalizeOptionalText(sellerName);
        Availability = NormalizerService.NormalizeOptionalText(availability);
        CategoriesStore =  NormalizerService.NormalizeCategories(categoriesStore);
    }
}