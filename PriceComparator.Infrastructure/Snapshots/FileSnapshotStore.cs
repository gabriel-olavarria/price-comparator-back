using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace PriceComparator.Infrastructure.Snapshots;

public sealed class FileSnapshotStore : ISnapshotStore
{
    private readonly string _rootDirectory;

    public FileSnapshotStore(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var configuredPath = configuration["Snapshots:RootPath"];

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException(
                "No se configuró Snapshots:RootPath.");
        }

        _rootDirectory = Path.GetFullPath(
            Path.Combine(
                environment.ContentRootPath,
                configuredPath));

        Directory.CreateDirectory(_rootDirectory);

        Console.WriteLine(
            $"[SNAPSHOT] Root directory: {_rootDirectory}");
    }

    public async Task SaveAsync(
        string storeCode,
        string query,
        string html,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storeCode))
        {
            throw new ArgumentException(
                "El código de la tienda es obligatorio.",
                nameof(storeCode));
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException(
                "La búsqueda es obligatoria.",
                nameof(query));
        }

        if (string.IsNullOrWhiteSpace(html))
        {
            throw new ArgumentException(
                "El HTML no puede estar vacío.",
                nameof(html));
        }

        var filePath = GetFilePath(
            storeCode,
            query);

        var directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        Console.WriteLine(
            $"[SNAPSHOT] Guardando: {filePath}");

        await File.WriteAllTextAsync(
            filePath,
            html,
            cancellationToken);

        Console.WriteLine(
            $"[SNAPSHOT] Snapshot guardado correctamente.");
    }

    private string GetFilePath(
        string storeCode,
        string query)
    {
        var normalizedStoreCode =
            NormalizeFileName(storeCode);

        var normalizedQuery =
            NormalizeFileName(query);

        var timestamp = DateTime.Now
            .ToString("yyyy-MM-dd_HH-mm-ss");

        var fileName =
            $"{normalizedQuery}_{timestamp}.html";

        return Path.Combine(
            _rootDirectory,
            normalizedStoreCode,
            fileName);
    }

    private static string NormalizeFileName(string value)
    {
        var normalized = value
            .Trim()
            .ToLowerInvariant();

        foreach (var invalidCharacter
                 in Path.GetInvalidFileNameChars())
        {
            normalized = normalized.Replace(
                invalidCharacter,
                '-');
        }

        return normalized.Replace(' ', '-');
    }
}