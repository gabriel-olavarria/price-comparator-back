using System.Text.Json;
using PriceComparator.Domain.Entities;

namespace PriceComparator.Infrastructure.Storage;

public sealed class JsonProductStore
{
    private readonly string _basePath;

    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

    public JsonProductStore()
    {
        _basePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..",
            "PriceComparator.Infrastructure",
            "Storage",
            "Data");
    }

    public async Task<IReadOnlyCollection<ProductOffer>?> GetAsync(
        string storeCode,
        string query,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(storeCode, query);

        Console.WriteLine(
            $"[STORAGE] Buscando JSON en: {filePath}");

        if (!File.Exists(filePath))
        {
            Console.WriteLine(
                $"[STORAGE] No existe JSON para {storeCode} - {query}.");

            return null;
        }

        try
        {
            await using var stream = File.OpenRead(filePath);

            var data = await JsonSerializer.DeserializeAsync<ProductStorage>(
                stream,
                JsonOptions,
                cancellationToken);

            if (data is null)
            {
                Console.WriteLine(
                    $"[STORAGE] El JSON de {storeCode} - {query} no contiene datos válidos.");

                return null;
            }

            if (data.GeneratedAt.Date != DateTime.UtcNow.Date)
            {
                Console.WriteLine(
                    $"[STORAGE] JSON expirado para {storeCode} - {query}. " +
                    $"Generado: {data.GeneratedAt:O}");

                return null;
            }

            if (data.Products.Count == 0)
            {
                Console.WriteLine(
                    $"[STORAGE] JSON sin productos para {storeCode} - {query}. " +
                    "Se consultará nuevamente.");

                return null;
            }

            Console.WriteLine(
                $"[STORAGE] JSON encontrado para {storeCode} - {query}: " +
                $"{data.Products.Count} productos.");

            return data.Products;
        }
        catch (JsonException exception)
        {
            Console.WriteLine(
                $"[STORAGE] Error leyendo JSON de {storeCode} - {query}: " +
                $"{exception.Message}");

            return null;
        }
    }

    public async Task SaveAsync(
        string storeCode,
        string query,
        IReadOnlyCollection<ProductOffer> products,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(
            storeCode,
            query);

        var directoryPath =
            Path.GetDirectoryName(filePath);

        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(
                directoryPath);
        }

        var data = new ProductStorage
        {
            StoreCode = storeCode,
            Query = query.Trim(),
            GeneratedAt = DateTime.UtcNow,
            Products = products
        };

        Console.WriteLine(
            $"[STORAGE] Guardando JSON en: {filePath}");

        await using var stream =
            File.Create(filePath);

        await JsonSerializer.SerializeAsync(
            stream,
            data,
            JsonOptions,
            cancellationToken);

        Console.WriteLine(
            $"[STORAGE] JSON guardado correctamente: " +
            $"{products.Count} productos.");
    }

    private string GetFilePath(
        string storeCode,
        string query)
    {
        var storePath = Path.Combine(
            _basePath,
            storeCode);

        var fileName =
            $"{Normalize(query)}.json";

        return Path.Combine(
            storePath,
            fileName);
    }

    private static string Normalize(
        string value)
    {
        return string.Join(
            "-",
            value
                .Trim()
                .ToLowerInvariant()
                .Split(
                    [' ', '/', '\\'],
                    StringSplitOptions.RemoveEmptyEntries));
    }
}