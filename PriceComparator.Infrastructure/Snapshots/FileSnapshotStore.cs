using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace PriceComparator.Infrastructure.Snapshots;

public sealed class FileSnapshotStore : ISnapshotStore
{
    private readonly string _rootDirectory;
    private readonly bool _isProduction;

    public FileSnapshotStore(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        _isProduction = environment.IsProduction();

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

        /*
         * Los snapshots son únicamente para desarrollo/diagnóstico.
         * En Production no creamos directorios ni archivos.
         */
        if (!_isProduction)
        {
            Directory.CreateDirectory(_rootDirectory);

            Console.WriteLine(
                $"[SNAPSHOT] Root directory: {_rootDirectory}");
        }
        else
        {
            Console.WriteLine(
                "[SNAPSHOT] Production - guardado de snapshots deshabilitado.");
        }
    }

    public async Task SaveAsync(
        string storeCode,
        string query,
        string html,
        CancellationToken cancellationToken = default)
    {
        /*
         * En Production ignoramos cualquier intento de guardar
         * snapshots realizado por los searchers.
         */
        if (_isProduction)
        {
            Console.WriteLine(
                $"[SNAPSHOT] Production - snapshot omitido para {storeCode}.");

            return;
        }

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
            "[SNAPSHOT] Snapshot guardado correctamente.");
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