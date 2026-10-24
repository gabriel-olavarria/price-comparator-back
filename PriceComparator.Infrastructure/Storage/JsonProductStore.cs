using System.Text.Json;
using Microsoft.Extensions.Options;
using PriceComparator.Domain.Entities;
using PriceComparator.Application.Interfaces.DependencyInjection;

namespace PriceComparator.Infrastructure.Storage;

public sealed class JsonProductStore : ISingletonDependency
{
    private readonly string _basePath;
    private readonly ProductStorageOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public JsonProductStore(ProductStorageOptions options)
    {
        _options = options;
        Console.WriteLine($"SaveRaw: {_options.SaveRaw}");
        Console.WriteLine($"SaveProcessed: {_options.SaveProcessed}");

        _basePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..",
            "PriceComparator.Infrastructure",
            "Storage",
            "Data");
    }

    public async Task<IReadOnlyCollection<Product>?> GetAsync(string storeCode, string query, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(storeCode, query, "Processed");

        // Compatibilidad con archivos anteriores
        if (!File.Exists(filePath))
        {
            filePath = GetLegacyFilePath(storeCode, query);
        }

        if (!File.Exists(filePath))
            return null;

        try
        {
            await using var stream = File.OpenRead(filePath);

            var data = await JsonSerializer.DeserializeAsync<ProductStorage>(stream, JsonOptions, cancellationToken);

            if (data is null)
                return null;

            if (data.GeneratedAt.Date != DateTime.UtcNow.Date)
                return null;

            if (data.Products.Count == 0)
                return null;

            return data.Products;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task SaveAsync(string storeCode, string query, IReadOnlyCollection<Product> products, CancellationToken cancellationToken = default)
    {
        if (!_options.SaveProcessed)
            return;

        var data = new ProductStorage
        {
            StoreCode = storeCode,
            Query = query.Trim(),
            GeneratedAt = DateTime.UtcNow,
            Products = products
        };

        var filePath = GetFilePath(storeCode, query, "Processed");

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, data, JsonOptions, cancellationToken);
    }

    public async Task SaveRawAsync(string storeCode, string query, string rawJson, CancellationToken cancellationToken = default)
    {
        if (!_options.SaveRaw)
            return;

        var filePath = GetFilePath(storeCode, query, "Raw");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, rawJson, cancellationToken);
    }

    private string GetFilePath(string storeCode, string query, string type)
    {
        return Path.Combine(_basePath, type, storeCode, $"{Normalize(query)}.json");
    }

    private string GetLegacyFilePath(string storeCode, string query)
    {
        return Path.Combine(_basePath, storeCode, $"{Normalize(query)}.json");
    }

    private static string Normalize(string value)
    {
        return string.Join("-", value.Trim().ToLowerInvariant().Split([' ', '/', '\\'], StringSplitOptions.RemoveEmptyEntries));
    }
}