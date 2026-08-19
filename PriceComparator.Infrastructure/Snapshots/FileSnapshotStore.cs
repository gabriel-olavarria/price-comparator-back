using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace PriceComparator.Infrastructure.Snapshots;

public sealed class FileSnapshotStore : ISnapshotStore
{
    private readonly string _rootDirectory;

    public FileSnapshotStore(IConfiguration configuration, IHostEnvironment environment)
    {
        var configuredPath = configuration["Snapshots:RootPath"];

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException("No se configuró Snapshots:RootPath.");
        }

        _rootDirectory = Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredPath));
        Directory.CreateDirectory(_rootDirectory);
        Console.WriteLine($"[SNAPSHOT] Root directory: {_rootDirectory}");
    }

    public async Task<string?> GetAsync(string storeCode, string query, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(storeCode, query);

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"[SNAPSHOT] No encontrado: {filePath}");
            return null;
        }

        Console.WriteLine($"[SNAPSHOT] Leyendo: {filePath}");
        return await File.ReadAllTextAsync(filePath, cancellationToken);
    }

    public async Task SaveAsync(string storeCode, string query, string html, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(storeCode, query);
        var directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        Console.WriteLine($"[SNAPSHOT] Guardando: {filePath}");

        await File.WriteAllTextAsync(filePath, html, cancellationToken);

        Console.WriteLine($"[SNAPSHOT] Guardado: {File.Exists(filePath)}");
    }

    private string GetFilePath(string storeCode, string query)
    {
        var fileName = NormalizeQuery(query);

        return Path.Combine(_rootDirectory, storeCode, $"{fileName}.html");
    }

    private static string NormalizeQuery(string query)
    {
        var normalized = query.Trim().ToLowerInvariant();

        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            normalized = normalized.Replace(invalidCharacter, '-');
        }

        return normalized.Replace(' ', '-');
    }
}