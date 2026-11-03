
using System.Text.Json;
using PriceComparator.Domain.Entities;
using PriceComparator.Domain.ValueObjects;
using Xunit;

namespace PriceComparator.Tests.Domain.Entities;

public sealed class ProductSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public void SerializeAndDeserialize_ShouldPreserveProductData()
    {
        // Arrange
        var product = new Product(
            name: "Arroz grado 1 1 kg",
            price: 1990,
            store: new Store
            {
                Code = "jumbo",
                Name = "Jumbo"
            },
            productUrl: new Uri("https://www.jumbo.cl/arroz"),
            imageUrl: new Uri("https://www.jumbo.cl/arroz.jpg"),
            brand: "Marca de prueba",
            sellerName: "Jumbo",
            categoriesStore: ["Abarrotes", "Arroz"],
            availability: "Disponible"
        )
        {
            CanonicalKey = "arroz-1kg",
            CanonicalName = "Arroz 1 kg",
            Score = 1500,

            Normalized = new Normalized
            {
                Query = "arroz",
                Name = "arroz grado 1 1 kg",
                Brand = "marca de prueba"
            },

            Clasification = new Clasification
            {
                Id = "arroz",
                Category = "Abarrotes",
                Subtype = "Grado 1",
                Measurement = new Measurement(
                    UnitType: null,
                    Quantity: 1,
                    QuantityUnit: 1,
                    Unit: null
                )
            }
        };

        // Act
        var json = JsonSerializer.Serialize(product, JsonOptions);

        var restored = JsonSerializer.Deserialize<Product>(
            json,
            JsonOptions);

        // Assert
        Assert.NotNull(restored);

        Assert.Equal(product.Name, restored.Name);
        Assert.Equal(product.Price, restored.Price);
        Assert.Equal(product.Store, restored.Store);
        Assert.Equal(product.ProductUrl, restored.ProductUrl);
        Assert.Equal(product.ImageUrl, restored.ImageUrl);
        Assert.Equal(product.Brand, restored.Brand);
        Assert.Equal(product.SellerName, restored.SellerName);
        Assert.Equal(product.Availability, restored.Availability);

        Assert.Equal(
            product.CategoriesStore,
            restored.CategoriesStore);

        Assert.Equal(product.CanonicalKey, restored.CanonicalKey);
        Assert.Equal(product.CanonicalName, restored.CanonicalName);
        Assert.Equal(product.Score, restored.Score);

        Assert.Equal(product.Normalized, restored.Normalized);
        Assert.Equal(product.Clasification, restored.Clasification);
    }

    [Fact]
    public void Deserialize_InvalidProduct_ShouldThrowException()
    {
        const string json = """
        {
            "name": "",
            "price": 0
        }
        """;

        Assert.ThrowsAny<Exception>(() =>
            JsonSerializer.Deserialize<Product>(
                json,
                JsonOptions));
    }
}
