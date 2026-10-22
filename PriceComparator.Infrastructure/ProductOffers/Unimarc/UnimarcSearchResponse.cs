using System.Text.Json.Serialization;

namespace PriceComparator.Infrastructure.ProductOffers.Unimarc.Models;

public sealed class UnimarcSearchResponse
{
    [JsonPropertyName("availableProducts")]
    public List<UnimarcApiProduct> AvailableProducts { get; set; } = [];
}

public sealed class UnimarcApiProduct
{
    [JsonPropertyName("item")]
    public UnimarcItem? Item { get; set; }

    [JsonPropertyName("price")]
    public UnimarcPrice? Price { get; set; }
}

public sealed class UnimarcItem
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("brand")]
    public string? Brand { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("images")]
    public List<string> Images { get; set; } = [];

    [JsonPropertyName("categories")]
    public List<string> Categories { get; set; } = [];
}

public sealed class UnimarcPrice
{
    [JsonPropertyName("price")]
    public string? Price { get; set; }

    [JsonPropertyName("listPrice")]
    public string? ListPrice { get; set; }

    [JsonPropertyName("availableQuantity")]
    public int AvailableQuantity { get; set; }
}